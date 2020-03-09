using System.Collections.Immutable;

using Inference.Common;

namespace Inference.Basic
{
    public interface ITerm : IHasVariables<string> { }

    public class TermVariable : ITerm
    {
        public string Name { get; }

        public TermVariable(string name) { this.Name = name; }

        public IImmutableSet<string> FreeVariables() =>
            ImmutableHashSet.Create(this.Name);

        public override string ToString() => this.Name;
    }

    public class LambdaAbstraction : ITerm
    {
        public string Parameter { get; }
        public ITerm Body { get; }

        public LambdaAbstraction(string param, ITerm body) { this.Parameter = param; this.Body = body; }

        public IImmutableSet<string> FreeVariables() =>
            this.Body.FreeVariables().Remove(this.Parameter);

        public override string ToString() => 
            $"\\{this.Parameter}.{this.Body}";
    }

    public class Application : ITerm
    {
        public ITerm Func { get; }
        public ITerm Arg { get; }

        public Application(ITerm func, ITerm arg) { this.Func = func; this.Arg = arg; }

        public IImmutableSet<string> FreeVariables() =>
            this.Func.FreeVariables().Union(this.Arg.FreeVariables());

        public override string ToString()
        {
            var func = (this.Func is LambdaAbstraction || this.Func is LetBinding) ? $"({this.Func})" : $"{this.Func}";
            var arg = (this.Arg is LambdaAbstraction || this.Arg is Application || this.Arg is LetBinding) ? $"({this.Arg})" : $"{this.Arg}";
            return $"{func} {arg}";
        }
    }

    public class LetBinding : ITerm
    {
        public string Binder { get; }
        public ITerm Bound { get; }
        public ITerm Expression { get; }

        public LetBinding(string binder, ITerm bound, ITerm expr) { this.Binder = binder; this.Bound = bound; this.Expression = expr; }

        public IImmutableSet<string> FreeVariables() =>
            this.Expression.FreeVariables()
                .Remove(this.Binder)
                .Union(this.Bound.FreeVariables());

        public override string ToString() => $"let {this.Binder} = {this.Bound} in {this.Expression}";
    }
}
