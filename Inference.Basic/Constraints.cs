using System;
using System.Collections.Immutable;
using System.Linq;

namespace Inference.Basic
{
    public interface IConstraint : IContextEntry
    {
        IImmutableList<IContextEntry> Solve(IImmutableList<IContextEntry> prefix, IImmutableList<IContextEntry> suffix);
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

        public IImmutableList<IContextEntry> Solve(IImmutableList<IContextEntry> prefix, IImmutableList<IContextEntry> suffix)
        {
            switch (this.Left, this.Right)
            {
                case (ArrowType aleft, ArrowType aright):
                    return prefix
                        .Add(new TypeConstraint(aleft.Input, aright.Input))
                        .Add(new TypeConstraint(aleft.Output, aright.Output))
                        .AddRange(suffix);
                case (TypeVariable vleft, TypeVariable vright):
                    var top = prefix[prefix.Count - 1];
                    var popped = prefix.RemoveAt(prefix.Count - 1);
                    return top switch
                    {
                        LocalityMarker _ => popped.Add(this).Add(top).AddRange(suffix),
                        TermVariableBinding _ => popped.Add(this).Add(top).AddRange(suffix),
                        TypeVariableIntro i => (vleft.Name == i.Name, vright.Name == i.Name) switch
                        {
                            (true, true) => popped.Add(top).AddRange(suffix),
                            (true, false) => popped.Add(new TypeVariableDefinition(vleft.Name, vright)).AddRange(suffix),
                            (false, true) => popped.Add(new TypeVariableDefinition(vright.Name, vleft)).AddRange(suffix),
                            (false, false) => popped.Add(this).Add(top).AddRange(suffix),
                        },
                        TypeVariableDefinition d => (vleft.Name == d.Name, vright.Name == d.Name) switch
                        {
                            (true, true) => popped.Add(top).AddRange(suffix),
                            _ => popped.Add(this.Substitute(d.Name, d.Definition)).Add(top).AddRange(suffix),
                        },
                        _ => throw new Exception($"Unknown context entry: {top}."),
                    };
                case (TypeVariable vleft, IType tright): return this.StepFlexRigid(vleft, tright, prefix, suffix);
                case (IType tleft, TypeVariable vright): return this.StepFlexRigid(vright, tleft, prefix, suffix);
                default: throw new Exception("Rigid-rigid mismatch");
            }
        }

        private IImmutableList<IContextEntry> StepFlexRigid(TypeVariable flex, IType rigid, IImmutableList<IContextEntry> prefix, IImmutableList<IContextEntry> suffix)
        {
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
