namespace Inference.Dots

module Kinds =
    
    type Kind =
        | Data
        | Arrow of Kind * Kind
        | Seq of Kind

    let rec prettyKind(k : Kind) =
        match k with
        | Data -> "*"
        | Arrow(f, a) -> "(" + prettyKind f + " -> " + prettyKind a + ")"
        | Seq(k) -> "[" + prettyKind k + "]"