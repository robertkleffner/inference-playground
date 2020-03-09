using Microsoft.VisualStudio.TestTools.UnitTesting;

using Inference.Common;

namespace Inference.Units.Test
{
    [TestClass]
    public class InferenceTests
    {
        [TestMethod]
        public void CoreHindleyMilnerTests()
        {
            var InitialState = new InferenceState(new FreshVariableStream());

            Assert.AreEqual(
                TypeInference.Infer(InitialState, new LambdaAbstraction("x", new TermVariable("x"))),
                new ArrowType(new TypeVariable("t0"), new TypeVariable("t0")));

            Assert.AreEqual(
                TypeInference.Infer(InitialState, new LambdaAbstraction("x", new LambdaAbstraction("y", new TermVariable("y")))),
                new ArrowType(new TypeVariable("t0"), new TypeVariable("t1"), new TypeVariable("t1")));

            Assert.AreEqual(
                TypeInference.Infer(InitialState, new LambdaAbstraction("x", new LambdaAbstraction("y", new TermVariable("x")))),
                new ArrowType(new TypeVariable("t0"), new TypeVariable("t1"), new TypeVariable("t0")));

            Assert.AreEqual(
                TypeInference.Infer(InitialState, new LambdaAbstraction("x", new Application(new LambdaAbstraction("y", new TermVariable("y")), new TermVariable("x")))),
                new ArrowType(new TypeVariable("t0"), new TypeVariable("t0")));

            Assert.AreEqual(
                TypeInference.Infer(InitialState, new LambdaAbstraction("x", new Application(new LambdaAbstraction("y", new TermVariable("x")), new TermVariable("x")))),
                new ArrowType(new TypeVariable("t0"), new TypeVariable("t0")));

            Assert.AreEqual(
                TypeInference.Infer(InitialState, new LetBinding("m", new LambdaAbstraction("a", new TermVariable("a")), new Application(new TermVariable("m"), new TermVariable("m")))),
                new ArrowType(new TypeVariable("t2"), new TypeVariable("t2")));

            Assert.AreEqual(
                TypeInference.Infer(InitialState,
                    new LetBinding("s", new LetBinding("m", new LambdaAbstraction("a", new TermVariable("a")), new Application(new TermVariable("m"), new TermVariable("m"))), new TermVariable("s"))),
                new ArrowType(new TypeVariable("t4"), new TypeVariable("t4")));

            Assert.AreEqual(
                TypeInference.Infer(InitialState, new LambdaAbstraction("x", new LambdaAbstraction("y", new Application(new TermVariable("x"), new TermVariable("y"))))),
                new ArrowType(new ArrowType(new TypeVariable("t1"), new TypeVariable("t2")), new TypeVariable("t1"), new TypeVariable("t2")));
        }

        [TestMethod]
        public void InferFloatTests()
        {
            InferenceState FloatTestInitialContext = new InferenceState(new FreshVariableStream(),
                new TermVariableBinding("zero", new TypeScheme(new FloatType(new UnitPower(new TypeVariable("x"), 1)), ("x", Kind.Unit))),
                new TermVariableBinding("one", new TypeScheme(new FloatType(new UnitIdentity()))),
                new TermVariableBinding("mass", new TypeScheme(new FloatType(new UnitPower(new PrimitiveUnit("kg"), 1)))),
                new TermVariableBinding("time", new TypeScheme(new FloatType(new UnitPower(new PrimitiveUnit("sec"), 1)))),
                new TermVariableBinding("mul", new TypeScheme(new ArrowType(
                    new FloatType(new UnitPower(new TypeVariable("a"), 1)),
                    new FloatType(new UnitPower(new TypeVariable("b"), 1)),
                    new FloatType(new UnitMultiply(new UnitPower(new TypeVariable("a"), 1), new UnitPower(new TypeVariable("b"), 1)))), ("a", Kind.Unit), ("b", Kind.Unit))),
                new TermVariableBinding("plus", new TypeScheme(new ArrowType(
                    new FloatType(new UnitPower(new TypeVariable("a"), 1)),
                    new FloatType(new UnitPower(new TypeVariable("a"), 1)),
                    new FloatType(new UnitPower(new TypeVariable("a"), 1))), ("a", Kind.Unit))),
                new TermVariableBinding("div", new TypeScheme(new ArrowType(
                    new FloatType(new UnitMultiply(new UnitPower(new TypeVariable("a"), 1), new UnitPower(new TypeVariable("b"), 1))),
                    new FloatType(new UnitPower(new TypeVariable("a"), 1)),
                    new FloatType(new UnitPower(new TypeVariable("b"), 1))),
                    ("a", Kind.Unit), ("b", Kind.Unit))),
                new TermVariableBinding("pair", new TypeScheme(new ArrowType(
                    new FloatType(new UnitPower(new TypeVariable("a"), 1)),
                    new FloatType(new UnitPower(new TypeVariable("b"), 1)),
                    new FloatType(new UnitPower(new TypeVariable("a"), 1)),
                    new FloatType(new UnitPower(new TypeVariable("b"), 1))),
                    ("a", Kind.Unit), ("b", Kind.Unit)))
            );

            Assert.AreEqual(
                TypeInference.Infer(FloatTestInitialContext, new TermVariable("zero")),
                new FloatType(new UnitPower(new TypeVariable("t0"), 1)));

            Assert.AreEqual(
                TypeInference.Infer(FloatTestInitialContext, new Application(new TermVariable("mul"), new TermVariable("mass"), new TermVariable("mass"))),
                new FloatType(new UnitPower(new PrimitiveUnit("kg"), 2)));

            Assert.AreEqual(
                TypeInference.Infer(FloatTestInitialContext,
                    new LambdaAbstraction("a",
                        new LambdaAbstraction("b",
                            new Application(new TermVariable("plus"), new TermVariable("a"), new TermVariable("b"))))),
                new ArrowType(new FloatType(new UnitPower(new TypeVariable("h0"), 1)),
                    new FloatType(new UnitPower(new TypeVariable("h0"), 1)),
                    new FloatType(new UnitPower(new TypeVariable("h0"), 1))));

            Assert.AreEqual(
                TypeInference.Infer(FloatTestInitialContext,
                    new LambdaAbstraction("x",
                        new LetBinding("d", new Application(new TermVariable("div"), new TermVariable("x")),
                            new Application(new TermVariable("pair"),
                                new Application(new TermVariable("d"), new TermVariable("mass")),
                                new Application(new TermVariable("d"), new TermVariable("time")))))),
                new ArrowType(new FloatType(new UnitPower(new TypeVariable("h0"), 1)),
                    new FloatType(new UnitMultiply(new UnitPower(new TypeVariable("h0"), 1), new UnitPower(new PrimitiveUnit("kg"), -1))),
                    new FloatType(new UnitMultiply(new UnitPower(new TypeVariable("h0"), 1), new UnitPower(new PrimitiveUnit("sec"), -1)))));

            Assert.AreEqual(
                TypeInference.Infer(FloatTestInitialContext,
                    new LetBinding("recip", new Application(new TermVariable("div"), new TermVariable("one")),
                        new Application(new TermVariable("pair"),
                            new Application(new TermVariable("recip"), new TermVariable("mass")),
                            new Application(new TermVariable("recip"), new TermVariable("time"))))),
                new ArrowType(new FloatType(new UnitPower(new PrimitiveUnit("kg"), -1)), new FloatType(new UnitPower(new PrimitiveUnit("sec"), -1))));
        }
    }
}
