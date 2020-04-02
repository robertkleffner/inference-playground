using Microsoft.VisualStudio.TestTools.UnitTesting;

using Inference.Common;

namespace Inference.Typeclasses.Test
{
    [TestClass]
    public class BasicTests
    {
        [TestMethod]
        public void CoreHindleyMilnerTests()
        {
            var InitialState = new InferenceState(new FreshVariableStream());

            Assert.AreEqual(
                TypeInference.Infer(InitialState, new LambdaAbstraction("x", new TermVariable("x"))),
                new QualifiedType(PrimType.Fun(new TypeVariable("t0", new DataKind()), new TypeVariable("t0", new DataKind()))));

            Assert.AreEqual(
                TypeInference.Infer(InitialState, new LambdaAbstraction("x", new LambdaAbstraction("y", new TermVariable("y")))),
                new QualifiedType(PrimType.Fun(new TypeVariable("t0", new DataKind()), new TypeVariable("t1", new DataKind()), new TypeVariable("t1", new DataKind()))));

            Assert.AreEqual(
                TypeInference.Infer(InitialState, new LambdaAbstraction("x", new LambdaAbstraction("y", new TermVariable("x")))),
                new QualifiedType(PrimType.Fun(new TypeVariable("t0", new DataKind()), new TypeVariable("t1", new DataKind()), new TypeVariable("t0", new DataKind()))));

            Assert.AreEqual(
                TypeInference.Infer(InitialState, new LambdaAbstraction("x", new Application(new LambdaAbstraction("y", new TermVariable("y")), new TermVariable("x")))),
                new QualifiedType(PrimType.Fun(new TypeVariable("t0", new DataKind()), new TypeVariable("t0", new DataKind()))));

            Assert.AreEqual(
                TypeInference.Infer(InitialState, new LambdaAbstraction("x", new Application(new LambdaAbstraction("y", new TermVariable("x")), new TermVariable("x")))),
                new QualifiedType(PrimType.Fun(new TypeVariable("t0", new DataKind()), new TypeVariable("t0", new DataKind()))));

            Assert.AreEqual(
                TypeInference.Infer(InitialState, new LetBinding("m", new LambdaAbstraction("a", new TermVariable("a")), new Application(new TermVariable("m"), new TermVariable("m")))),
                new QualifiedType(PrimType.Fun(new TypeVariable("t2", new DataKind()), new TypeVariable("t2", new DataKind()))));

            Assert.AreEqual(
                TypeInference.Infer(InitialState,
                    new LetBinding("s", new LetBinding("m", new LambdaAbstraction("a", new TermVariable("a")), new Application(new TermVariable("m"), new TermVariable("m"))), new TermVariable("s"))),
                new QualifiedType(PrimType.Fun(new TypeVariable("t4", new DataKind()), new TypeVariable("t4", new DataKind()))));

            Assert.AreEqual(
                TypeInference.Infer(InitialState, new LambdaAbstraction("x", new LambdaAbstraction("y", new Application(new TermVariable("x"), new TermVariable("y"))))),
                new QualifiedType(PrimType.Fun(PrimType.Fun(new TypeVariable("t1", new DataKind()), new TypeVariable("t2", new DataKind())), new TypeVariable("t1", new DataKind()), new TypeVariable("t2", new DataKind()))));
        }
    }
}
