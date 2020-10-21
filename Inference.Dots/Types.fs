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

        member self.Substitute(target: string, sub: Type) =
            match self with
            | TVar(n, _, []) -> if n = target then sub else self
            | TVar(n, _, inds) -> if n = target then sub else self // TODO: select based on inds here
            | TSeq(ts) ->
                [ for t in ts do yield t.Substitute(target, sub) ]
                |> TSeq
            | TApp(l,r) ->
                match (l.Substitute(target, sub), r.Substitute(target, sub)) with
                | (TSeq ls, TSeq rs) -> null // TODO: zip all the way down here
                | (TSeq ls, r) -> TSeq [for t in ls do yield TApp(t, r)]
                | (l, TSeq rs) -> TSeq [for t in rs do yield TApp(l, t)]
                | (l, r) -> TApp(l, r)

        interface IHasVars with
            member self.Free() : string Set =
                match self with
                | TVar(n, _, _) -> Set.singleton(n)
                | TCon(_, _) -> Set.empty<string>
                | TApp(l, r) -> (l :> IHasVars).Free() + (r :> IHasVars).Free()
                | TSeq(ts) ->
                    [ for t in ts do yield (t :> IHasVars).Free() ]
                    |> Set.unionMany
                | TDot(t) -> (t :> IHasVars).Free()

    let snoc ls e = List.append ls [e]

    let zipwith (l1 : 'a list) (l2 : 'b list) (f : 'a -> 'b -> 'c) =
        List.zip l1 l2 |> List.map (fun (l,r) -> f l r)

    let ziplists (lists : 'a list list) =
        List.fold (fun l r -> zipwith l r snoc) (List.replicate (List.min (List.map (fun (l : 'a list) -> l.Length) lists)) List.empty) lists

    let rec extend (t: Type) (len: int) =
        match t with
        | TVar(n, k, inds) -> [for i in 0..len do yield TVar(n, k, List.append inds [i])]
        | TCon(_, _) -> [for i in 0..len do yield t]
        | TApp(l, r) -> zipwith (extend l len) (extend r len) (fun l r -> TApp(l, r))
        | TSeq(ts) -> List.concat [for t in ts do yield extend t len]
        | TDot(t) -> [for t' in extend t len do yield TDot t']

    let rec extendList (ts: Type list) (len: int) =
        match ts with
        | (TSeq tes) :: ts' -> tes :: extendList ts' len
        | t :: ts' -> extend t len :: extendList ts' len
        | [] -> []

    let sublen (t: Type) =
        match t with
        | TSeq ts -> ts.Length
        | _ -> 0

    // TODO: handle constructors that take sequences
    let rec liftSeqs(t : Type) =
        match t with
        | TApp (l, r) ->
            match (liftSeqs l, liftSeqs r) with
            | (TSeq ls, TSeq rs) -> TSeq (zipwith ls rs (fun l r -> TApp(l, r))) |> liftSeqs
            | (TSeq ls, r) -> TSeq (zipwith ls (extend r ls.Length) (fun l r -> TApp(l, r))) |> liftSeqs
            | (l, TSeq rs) -> TSeq (zipwith (extend l rs.Length) rs (fun l r -> TApp(l, r))) |> liftSeqs
            | (l, r) -> TApp (l, r)
        | TSeq ts ->
            let pushed = List.map liftSeqs ts
            let len = List.map sublen pushed |> List.max
            extendList pushed len
            |> ziplists
            |> List.map (fun t -> TSeq t)
            |> TSeq
            

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
        | TSeq(ts) -> Seq Data
        | TDot(t) -> kind t