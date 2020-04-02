using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Inference.Common;

namespace Inference.Typeclasses.Test
{
    [TestClass]
    public class TypeclassTests
    {
        [TestMethod]
        public void TestTypeclasses()
        {
            var initialState = new InferenceState(new FreshVariableStream(),
                new TypeclassDeclaration("Eq",
                    new TypeScheme(new TypeConstructor("int", new DataKind())),
                    new TypeScheme(new QualifiedType(
                        new TypeApplication(new TypeConstructor("[]", new ArrowKind(new DataKind(), new DataKind())),
                            new TypeVariable("a", new DataKind())),
                        new Predicate("Eq", new TypeVariable("a", new DataKind()))), ("a", new DataKind()))),
                new TypeclassDeclaration("Ord"),
                new TypeclassDeclaration("Show"),
                new TypeclassDeclaration("Read"),
                new TypeclassDeclaration("Bounded"),
                new TypeclassDeclaration("Enum"),
                new TypeclassDeclaration("Functor"),
                new TypeclassDeclaration("Monad"),
                new TermVariableBinding("eq", new TypeScheme(new QualifiedType(
                    PrimType.Fun(new TypeVariable("a", new DataKind()), new TypeVariable("a", new DataKind()), new TypeConstructor("bool", new DataKind())),
                    new Predicate("Eq", new TypeVariable("a", new DataKind()))),
                    ("a", new DataKind()))),
                new TermVariableBinding("zero", new TypeScheme(new QualifiedType(new TypeConstructor("int", new DataKind())))),
                new TermVariableBinding("true", new TypeScheme(new QualifiedType(new TypeConstructor("bool", new DataKind())))));

            Assert.AreEqual(
                TypeInference.Infer(initialState, new TermVariable("eq")),
                new QualifiedType(PrimType.Fun(new TypeVariable("t0", new DataKind()), new TypeVariable("t0", new DataKind()), new TypeConstructor("bool", new DataKind())),
                    new Predicate("Eq", new TypeVariable("t0", new DataKind()))));

            Assert.AreEqual(
                TypeInference.Infer(initialState, new Application(new TermVariable("eq"), new TermVariable("zero"))),
                new QualifiedType(PrimType.Fun(new TypeConstructor("int", new DataKind()), new TypeConstructor("bool", new DataKind()))));

            Assert.ThrowsException<Exception>(() =>
                TypeInference.Infer(initialState, new Application(new TermVariable("eq"), new TermVariable("true"))));
        }
    }
}
