namespace Inference.Dots

module Types =
    open Kinds

    type IHasVars =
        abstract member Free : unit -> string Set

    type Type =
        | TVar of name: string * kind: Kind * indexes: int list
        | TCon of name: string * kind: Kind
        | TApp of left: Type * right: Type
        | TSeq of types: Type list
        | TDot of dotted: Type

    let isSeq ty =
        match ty with
        | TSeq _ -> true
        | _ -> false

    let isNotSeq ty =
        match ty with
        | TSeq _ -> false
        | _ -> true

    let dvar name = TVar (name, Data, [])

    let divar name inds = TVar (name, Data, inds)

module Substitution =
    open Kinds
    open Types

    let rec free (t : Type) =
        match t with
        | TVar(n, _, _) -> Set.singleton(n)
        | TCon(_, _) -> Set.empty<string>
        | TApp(l, r) -> free l + free r
        | TSeq(ts) ->
            [ for t in ts do yield free t ]
            |> Set.unionMany
        | TDot(t) -> free t

    let rec getsub (inds : int list) (sub : Type) =
        match (inds, sub) with
        | ([], s) -> s
        | ([i], TSeq ts) -> ts.[i]
        | (i :: is, TSeq ts) -> getsub is ts.[i]
        | _ -> failwith "Invalid pattern in index substitution"

    let rec spliceDots seq =
        match seq with
        | [] -> []
        | TDot (TSeq ts) :: rest -> List.append ts (spliceDots rest)
        | t :: rest -> t :: spliceDots rest

    let snoc ls e = List.append ls [e]
    
    let zipwith (l1 : 'a list) (l2 : 'b list) (f : 'a -> 'b -> 'c) =
        List.zip l1 l2 |> List.map (fun (l,r) -> f l r)

    let zipn items = 
        Seq.collect Seq.indexed items
        |> Seq.groupBy fst 
        |> Seq.map (snd >> Seq.map snd)
        |> Seq.map Seq.toList
        |> Seq.toList

    let ziplists (lists : 'a list list) =
        List.fold (fun l r -> zipwith l r snoc) (List.replicate (List.min (List.map (fun (l : 'a list) -> l.Length) lists)) List.empty) lists
    
    let rec extend (t: Type) (len: int) =
        match t with
        | TVar(n, k, inds) -> [for i in 0..len-1 do yield TVar(n, k, List.append inds [i])]
        | TCon(_, _) -> [for i in 0..len-1 do yield t]
        | TApp(l, r) -> zipwith (extend l len) (extend r len) (fun l r -> TApp(l, r))
        | TSeq(ts) -> List.concat [for t in ts do yield extend t len]
        | TDot(t) -> [for t' in extend t len do yield TDot t']
    
    let rec extendList (ts: Type list) (len: int) =
        match ts with
        | TSeq tes :: ts' -> tes :: extendList ts' len
        | TDot (TSeq tes) :: ts' -> ts :: extendList ts' len
        | t :: ts' -> extend t len :: extendList ts' len
        | [] -> []
    
    let sublen (t: Type) =
        match t with
        | TSeq ts -> ts.Length
        | TDot (TSeq ts) -> ts.Length
        | _ -> 1
    
    // TODO: handle constructors that take sequences
    let rec liftSeqs(t : Type) =
        match t with
        | TApp (l, r) ->
            match (liftSeqs l, liftSeqs r) with
            | (TSeq ls, TSeq rs) -> TSeq (zipwith ls rs (fun l r -> TApp(l, r) |> liftSeqs))
            | (TSeq ls, r) -> TSeq (zipwith ls (extend r ls.Length) (fun l r -> TApp(l, r) |> liftSeqs))
            | (TCon (n, Arrow(Seq Data, k)), TSeq rs) -> failwith "TODO: Need to handle type constructors that take sequence args"
            | (l, TSeq rs) -> TSeq (zipwith (extend l rs.Length) rs (fun l r -> TApp(l, r) |> liftSeqs))
            | (l, TDot r) -> TApp (l, r) |> liftSeqs |> TDot
            | (TDot l, r) -> TApp (l, r) |> liftSeqs |> TDot
            | (l, r) -> TApp (l, r)
        | TSeq ts when List.exists isSeq ts && List.exists isNotSeq ts ->
            let pushed = List.map liftSeqs ts
            let len = List.map sublen pushed |> List.max
            let zipped = extendList pushed len |> zipn
            zipped
            |> List.map (fun t -> TSeq t)
            |> TSeq
        | TSeq ts -> List.map liftSeqs ts |> TSeq
        | TDot d -> liftSeqs d |> TDot
        | any -> any

    let rec substitute (rep : Type) (target: string) (sub: Type) =
        match rep with
        | TCon(_, _) -> rep
        | TVar(n, _, inds) -> if n = target then getsub inds sub else rep
        | TSeq(ts) ->
            [ for t in ts do yield substitute t target sub ]
            |> spliceDots
            |> TSeq
            |> liftSeqs
        | TApp(l,r) -> TApp (substitute l target sub, substitute r target sub) |> liftSeqs
        | TDot d -> TDot (substitute d target sub) |> liftSeqs
            

    let rec isHeadNormalForm(t : Type) =
        match t with
        | TVar(_, _, _) -> true
        | TApp (left, _) -> isHeadNormalForm left
        | _ -> false

    let rec kind(t : Type) =
        match t with
        | TVar(_, k, _) -> k
        | TCon(_, k) -> k
        | TApp(l, r) ->
            match kind l with
            | Arrow(al, ar) -> if (ar = kind r) then ar else failwith "Kind error: tried to apply type constructor to arg of wrong kind"
            | _ -> failwith "Kind error: type constructor requires arrow kind"
        | TSeq _ -> Seq Data
        | TDot t -> kind t