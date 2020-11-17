namespace Inference.Dots.Test

open System
open Microsoft.VisualStudio.TestTools.UnitTesting
open Inference.Dots.Kinds
open Inference.Dots.Types
open Inference.Dots.Substitution

[<TestClass>]
type TestClass () =

    let lty var = TApp (TCon ("List", Arrow(Data, Data)), TVar (var, Data, []))
    let idMap var = TApp (TApp (TCon ("Map", Arrow(Data, Arrow(Data, Data))), TVar (var, Data, [])), TVar (var, Data, []))

    [<TestMethod>]
    member this.FreeTests() =
        Assert.AreEqual(Set.empty<string>, TCon ("a", Data) |> free)
        Assert.AreEqual(Set.singleton "a", TVar ("a", Data, []) |> free)

    [<TestMethod>]
    member this.ExtendTests () =
        Assert.AreEqual(
            [TApp (TCon ("a", Data), TVar ("b", Data, [0]));
             TApp (TCon ("a", Data), TVar ("b", Data, [1]));
             TApp (TCon ("a", Data), TVar ("b", Data, [2]))],
            extend (TApp (TCon ("a", Data), TVar ("b", Data, []))) 3)

        Assert.AreEqual(
            [TApp (TCon ("a", Data), TVar ("b", Data, [2;0]));
             TApp (TCon ("a", Data), TVar ("b", Data, [2;1]));
             TApp (TCon ("a", Data), TVar ("b", Data, [2;2]))],
            extend (TApp (TCon ("a", Data), TVar ("b", Data, [2]))) 3)

        Assert.AreEqual(
            [[TApp (TCon ("a", Data), TVar ("b", Data, [0]));
              TApp (TCon ("a", Data), TVar ("b", Data, [1]));
              TApp (TCon ("a", Data), TVar ("b", Data, [2]))];
             [TApp (TCon ("c", Data), TVar ("d", Data, [0]))
              TApp (TCon ("c", Data), TVar ("d", Data, [1]))
              TApp (TCon ("c", Data), TVar ("d", Data, [2]))]],
            extendList [TApp (TCon ("a", Data), TVar ("b", Data, [])); TApp (TCon ("c", Data), TVar ("d", Data, []))] 3)

    [<TestMethod>]
    member this.ListSubstitutionTest () =
        Assert.AreEqual(
            TSeq [
                TSeq [lty "c"; lty "d"; lty "e"];
                TDot (TSeq [lty "f"; TDot (lty "g")])],
            substitute (lty "b") "b" (TSeq [TSeq [dvar "c"; dvar "d"; dvar "e"]; TDot (TSeq [dvar "f"; TDot (dvar "g")])]))

    [<TestMethod>]
    member this.MapSubsitutionTestSameKeyValue () =
        Assert.AreEqual(
            TSeq [
                TSeq [idMap "c"; idMap "d"; idMap "e"];
                TSeq [idMap "f"; TDot (idMap "g")]],
            substitute (idMap "b") "b" (TSeq [TSeq [dvar "c"; dvar "d"; dvar "e"]; TDot (TSeq [dvar "f"; TDot (dvar "g")])]))

    [<TestMethod>]
    member this.DotSubstitutionTests () =
        Assert.AreEqual(
            TSeq [dvar "b"; dvar "d"; dvar "e"; dvar "f" |> TDot],
            substitute (TSeq [dvar "b"; TDot (dvar "c")]) "c" (TSeq [dvar "d"; dvar "e"; TDot (dvar "f")]))

        Assert.AreEqual(
            TSeq [TSeq [divar "b" [0]; dvar "d"; dvar "e"; dvar "f" |> TDot]; TSeq [divar "b" [1]; dvar "g"; dvar "h"; dvar "i" |> TDot]],
            substitute (TSeq [dvar "b"; TDot (dvar "c")]) "c" (TSeq [TSeq [dvar "d"; dvar "e"; TDot (dvar "f")]; TDot (TSeq [dvar "g"; dvar "h"; TDot (dvar "i")])]))

    [<TestMethod>]
    member this.SequenceSubstitutionTest () =
        Assert.AreEqual(
            TSeq [TSeq [TSeq [dvar "d"; dvar "d"; TDot (TVar ("c", Data, [0;0]))];
                        TSeq [dvar "f"; dvar "f"; TDot (TVar ("c", Data, [0;1]))]];
                  TDot (TSeq [TDot (TSeq [dvar "g"; dvar "g"; TDot (TVar ("c", Data, [0; 1]))])])],
            substitute
                (TSeq [dvar "b"; dvar "b"; TDot (dvar "c")])
                "b"
                (TSeq [TSeq [dvar "d"; dvar "f"]; TDot (TSeq [TDot (dvar "g")])]))