using System;
using System.Collections.Immutable;
using System.Linq;

using Inference.Common;

namespace Inference.Typeclasses
{
    public interface IConstraint : IContextEntry
    {
        InferenceState Solve(FreshVariableStream fresh, IImmutableList<IContextEntry> prefix, IImmutableList<IContextEntry> suffix);
    }

    public class TypeConstraint : IConstraint
    {
        public IImmutableList<IContextEntry> Dependencies { get; }
        public IType Left { get; }
        public IType Right { get; }

        public TypeConstraint(IType left, IType right)
        {
            this.Dependencies = ImmutableList.Create<IContextEntry>();
            this.Left = left;
            this.Right = right;
        }

        public TypeConstraint(IImmutableList<IContextEntry> dependencies, IType left, IType right)
        {
            this.Dependencies = dependencies;
            this.Left = left;
            this.Right = right;
        }

        public TypeConstraint Substitute(string name, IType subWith) =>
            new TypeConstraint(this.Left.Substitute(name, subWith), this.Right.Substitute(name, subWith));

        public override string ToString() => $"[{string.Join(", ", this.Dependencies.Select(c => c.ToString()))} | {this.Left} == {this.Right}]";

        public InferenceState Solve(FreshVariableStream fresh, IImmutableList<IContextEntry> prefix, IImmutableList<IContextEntry> suffix)
        {
            switch (this.Left, this.Right)
            {
                case (TypeConstructor tleft, TypeConstructor tright):
                    if (tleft.Name != tright.Name)
                    {
                        throw new Exception("Rigid-rigid mismatch");
                    }
                    return new InferenceState(fresh, prefix.AddRange(suffix));
                case (TypeApplication aleft, TypeApplication aright):
                    return new InferenceState(fresh, prefix
                        .Add(new TypeConstraint(aleft.Func, aright.Func))
                        .Add(new TypeConstraint(aleft.Arg, aright.Arg))
                        .AddRange(suffix));
                case (TypeVariable vleft, TypeVariable vright):
                    var top = prefix[prefix.Count - 1];
                    var popped = prefix.RemoveAt(prefix.Count - 1);
                    return top switch
                    {
                        LocalityMarker _ => new InferenceState(fresh, popped.Add(this).Add(top).AddRange(suffix)),
                        TermVariableBinding _ => new InferenceState(fresh, popped.Add(this).Add(top).AddRange(suffix)),
                        TypeclassDeclaration _ => new InferenceState(fresh, popped.Add(this).Add(top).AddRange(suffix)),
                        TypeVariableIntro i => (vleft.Name == i.Name, vright.Name == i.Name) switch
                        {
                            (true, true) => new InferenceState(fresh, popped.Add(top).AddRange(suffix)),
                            (true, false) => new InferenceState(fresh, popped.Add(new TypeVariableDefinition(vleft.Name, vright)).AddRange(suffix)),
                            (false, true) => new InferenceState(fresh, popped.Add(new TypeVariableDefinition(vright.Name, vleft)).AddRange(suffix)),
                            (false, false) => new InferenceState(fresh, popped.Add(this).Add(top).AddRange(suffix)),
                        },
                        TypeVariableDefinition d => (vleft.Name == d.Name, vright.Name == d.Name) switch
                        {
                            (true, true) => new InferenceState(fresh, popped.Add(top).AddRange(suffix)),
                            _ => new InferenceState(fresh, popped.Add(this.Substitute(d.Name, d.Definition)).Add(top).AddRange(suffix)),
                        },
                        _ => throw new Exception($"Unknown context entry: {top}."),
                    };
                case (TypeVariable vleft, IType tright): return new InferenceState(fresh, this.StepFlexRigid(vleft, tright, prefix, suffix));
                case (IType tleft, TypeVariable vright): return new InferenceState(fresh, this.StepFlexRigid(vright, tleft, prefix, suffix));
                default: throw new Exception("Rigid-rigid mismatch");
            }
        }

        private IImmutableList<IContextEntry> StepFlexRigid(TypeVariable flex, IType rigid, IImmutableList<IContextEntry> prefix, IImmutableList<IContextEntry> suffix)
        {
            if (!flex.Kind.Equals(rigid.Kind))
            {
                throw new Exception("Kinds do not unify.");
            }
            var top = prefix[prefix.Count - 1];
            var popped = prefix.RemoveAt(prefix.Count - 1);
            switch (top)
            {
                case LocalityMarker _: return popped.Add(this).Add(top).AddRange(suffix);
                case TermVariableBinding _: return popped.Add(this).Add(top).AddRange(suffix);
                case TypeVariableDefinition d:
                    return popped.AddRange(this.Dependencies).Add(this.Substitute(d.Name, d.Definition)).Add(top).AddRange(suffix);
                case TypeVariableIntro i:
                    switch (i.Name == flex.Name, rigid.FreeVariables().Contains(i.Name))
                    {
                        case (true, true): throw new Exception("Occurs check failed!");
                        case (true, false): return popped.AddRange(this.Dependencies).Add(new TypeVariableDefinition(i.Name, rigid)).AddRange(suffix);
                        case (false, true): return popped.Add(new TypeConstraint(this.Dependencies.Insert(0, i), this.Left, this.Right)).AddRange(suffix);
                        case (false, false): return popped.Add(this).Add(top).AddRange(suffix);
                    }
                default: throw new Exception($"Unknown context entry: {top}");
            }
        }
    }
}
