using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Inference.Basic
{
    public class UnificationMachine
    {
        private IImmutableList<IContextEntry> _entries;

        public UnificationMachine(params IContextEntry[] entries)
        {
            this._entries = entries.ToImmutableList();
        }

        public UnificationMachine(IReadOnlyList<IContextEntry> entries)
        {
            this._entries = entries.ToImmutableList();
        }

        public bool CanStep() => this._entries.Any(e => e is TypeConstraint);

        public IImmutableList<IContextEntry> Run() => this.Run(out var _);

        public IImmutableList<IContextEntry> Run(out IList<UnificationMachine> steps)
        {
            var context = this;
            steps = new List<UnificationMachine> { this };
            while (context.CanStep())
            {
                context = context.Step();
                steps.Add(context);
            }
            return context._entries;
        }

        public UnificationMachine Step()
        {
            var eqContext = this._entries.TakeWhile(e => !(e is TypeConstraint));
            var eqAndRest = this._entries.SkipWhile(e => !(e is TypeConstraint));
            var eq = eqAndRest.First() as TypeConstraint;
            var rest = eqAndRest.Skip(1);

            switch (eq.Left, eq.Right)
            {
                case (ArrowType aleft, ArrowType aright):
                    var entries = eqContext
                        .Append(new TypeConstraint(aleft.Input, aright.Input))
                        .Append(new TypeConstraint(aleft.Output, aright.Output))
                        .Concat(rest);
                    return new UnificationMachine(entries.ToList());
                case (TypeVariable vleft, TypeVariable vright):
                    var top = eqContext.Last();
                    var popped = eqContext.SkipLast(1);
                    return top switch
                    {
                        LocalityMarker _ => new UnificationMachine(popped.Append(eq).Append(top).Concat(rest).ToList()),
                        TermVariableBinding _ => new UnificationMachine(popped.Append(eq).Append(top).Concat(rest).ToList()),
                        TypeVariableIntro i => (vleft.Name == i.Name, vright.Name == i.Name) switch
                        {
                            (true, true) => new UnificationMachine(popped.Append(top).Concat(rest).ToList()),
                            (true, false) => new UnificationMachine(popped.Append(new TypeVariableDefinition(vleft.Name, vright)).Concat(rest).ToList()),
                            (false, true) => new UnificationMachine(popped.Append(new TypeVariableDefinition(vright.Name, vleft)).Concat(rest).ToList()),
                            (false, false) => new UnificationMachine(popped.Append(eq).Append(top).Concat(rest).ToList()),
                        },
                        TypeVariableDefinition d => (vleft.Name == d.Name, vright.Name == d.Name) switch
                        {
                            (true, true) => new UnificationMachine(popped.Append(top).Concat(rest).ToList()),
                            _ => new UnificationMachine(popped.Append(eq.Substitute(d.Name, d.Definition)).Append(top).Concat(rest).ToList()),
                        },
                        _ => throw new Exception($"Unknown context entry: {top}."),
                    };
                case (TypeVariable vleft, IType tright): return StepFlexRigid(vleft, tright);
                case (IType tleft, TypeVariable vright): return StepFlexRigid(vright, tleft);
                default: throw new Exception("Rigid-rigid mismatch");
            }
        }

        private UnificationMachine StepFlexRigid(TypeVariable flex, IType rigid)
        {
            var eqContext = this._entries.TakeWhile(e => !(e is TypeConstraint));
            var eqAndRest = this._entries.SkipWhile(e => !(e is TypeConstraint));
            var rest = eqAndRest.Skip(1);

            var top = eqContext.Last();
            var popped = eqContext.SkipLast(1);
            if (eqAndRest.First() is TypeConstraint eq)
            {
                switch (top)
                {
                    case LocalityMarker _: return new UnificationMachine(popped.Append(eq).Append(top).Concat(rest).ToList());
                    case TermVariableBinding _: return new UnificationMachine(popped.Append(eq).Append(top).Concat(rest).ToList());
                    case TypeVariableDefinition d:
                        return new UnificationMachine(popped.Concat(eq.Dependencies).Append(eq.Substitute(d.Name, d.Definition)).Append(top).Concat(rest).ToList());
                    case TypeVariableIntro i:
                        switch (i.Name == flex.Name, rigid.FreeVariables().Contains(i.Name))
                        {
                            case (true, true): throw new Exception("Occurs check failed!");
                            case (true, false): return new UnificationMachine(popped.Concat(eq.Dependencies).Append(new TypeVariableDefinition(i.Name, rigid)).Concat(rest).ToList());
                            case (false, true): return new UnificationMachine(popped.Append(new TypeConstraint(eq.Dependencies.Insert(0, i), eq.Left, eq.Right)).Concat(rest).ToList());
                            case (false, false): return new UnificationMachine(popped.Append(eq).Append(top).Concat(rest).ToList());
                        }
                    default: throw new Exception($"Unknown context entry: {top}");
                }
            }
            else
            {
                throw new Exception("Was able to step without any equation!");
            }
        }

        public override string ToString() => string.Join(", ", this._entries.Select(e => e.ToString()));
    }
}
