using Microsoft.VisualStudio.TestTools.UnitTesting;

using Inference.Common;

namespace Inference.Basic.Test
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
    }
}
