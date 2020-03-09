using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Inference.Common;

namespace Inference.Units
{
    public class InferenceState
    {
        public IImmutableList<IContextEntry> Context { get; }
        public FreshVariableStream Fresh { get; }

        public InferenceState(FreshVariableStream fresh, IImmutableList<IContextEntry> context)
        {
            this.Fresh = fresh;
            this.Context = context;
        }

        public InferenceState(FreshVariableStream fresh, params IContextEntry[] context)
        {
            this.Fresh = fresh;
            this.Context = context.ToImmutableList();
        }

        public InferenceState Unify(out IImmutableList<InferenceState> states)
        {
            var state = this;
            states = ImmutableList.Create(state);
            while (state.Context.Any(e => e is IConstraint))
            {
                var prefix = state.Context.TakeWhile(e => !(e is IConstraint));
                var eqAndSuffix = state.Context.SkipWhile(e => !(e is IConstraint));
                if (eqAndSuffix.First() is IConstraint constraint)
                {
                    state = constraint.Solve(state.Fresh, prefix.ToImmutableList(), eqAndSuffix.Skip(1).ToImmutableList());
                }
                else
                {
                    throw new Exception("Should never get here: said there was a constraint, but then didn't find it.");
                }
                states = states.Add(state);
            }
            return state;
        }

        public InferenceState Update(IImmutableList<IContextEntry> context) => new InferenceState(this.Fresh, context);

        public InferenceState Push(IContextEntry entry) => new InferenceState(this.Fresh, this.Context.Add(entry));

        public override string ToString() => string.Join(", ", Context);
    }

    public class TypeInference
    {
        private readonly InferenceState _state;

        private TypeInference()
        {
            this._state = new InferenceState(new FreshVariableStream(), ImmutableList<IContextEntry>.Empty);
        }

        private TypeInference(InferenceState inference)
        {
            this._state = inference;
        }

        private TypeInference(FreshVariableStream fresh, IImmutableList<IContextEntry> context)
        {
            this._state = new InferenceState(fresh, context);
        }

        public static IType Infer(InferenceState initial, ITerm term)
        {
            var inference = new TypeInference(initial).Infer(term, out var result);
            Console.WriteLine($"Final: {inference}");
            var normalized = inference._state.Context.Normalize().Apply(result);
            return normalized;
        }

        private TypeInference Fresh(out string name)
        {
            var newStream = this._state.Fresh.Next("t", out name);
            return new TypeInference(newStream, this._state.Context);
        }

        private TypeInference AddIntro(string name, Kind kind) =>
            new TypeInference(this._state.Push(new TypeVariableIntro(name, kind)));

        private TypeInference AddBinding(string name, TypeScheme type) =>
            new TypeInference(this._state.Push(new TermVariableBinding(name, type)));

        private TypeInference PopBinding(string name)
        {
            var binding = this._state.Context.Last(c => (c is TermVariableBinding b) && b.Name == name);
            return new TypeInference(this._state.Fresh, this._state.Context.Remove(binding));
        }

        private TypeInference AddMark() =>
            new TypeInference(this._state.Push(new LocalityMarker()));

        private TypeInference Specialize(TypeScheme scheme, out IType specialized)
        {
            var state = this;
            specialized = scheme.Body;
            foreach (var (name, kind) in scheme.Quantified)
            {
                state = state
                    .Fresh(out var fresh)
                    .AddIntro(fresh, kind);
                specialized = specialized.Substitute(name, new TypeVariable(fresh));
            }
            return state;
        }

        private TypeInference InferGeneralized(ITerm term, out TypeScheme generalized)
        {
            var state = this
                .AddMark()
                .Infer(term, out var ungeneralized)
                .SkimContext(out var skimmed);
            generalized = this.MakeScheme(skimmed, ungeneralized);
            return state;
        }

        private TypeInference SkimContext(out IImmutableList<(string name, Kind kind, IType? definition)> skimmed)
        {
            skimmed = ImmutableList<(string name, Kind kind, IType? definition)>.Empty;
            var last = this._state.Context.Last();
            var newContext = this._state.Context.RemoveAt(this._state.Context.Count - 1);
            while (!(last is LocalityMarker))
            {
                skimmed = last switch
                {
                    TypeVariableIntro intro => skimmed.Add((intro.Name, intro.Kind, null)),
                    TypeVariableDefinition def => skimmed.Add((def.Name, def.Kind, def.Definition)),
                    _ => throw new Exception($"Unexpected entry in skimmed context: {last}"),
                };
                last = newContext.Last();
                newContext = newContext.RemoveAt(newContext.Count - 1);
            }
            return new TypeInference(this._state.Fresh, newContext);
        }

        private TypeScheme MakeScheme(IReadOnlyList<(string name, Kind kind, IType? definition)> skimmedFromContext, IType ungeneralized)
        {
            var generalized = ungeneralized;
            var quantified = new List<(string, Kind)>();
            foreach (var (typeVarName, kind, typeVarDef) in skimmedFromContext)
            {
                if (typeVarDef != null)
                {
                    generalized = generalized.Substitute(typeVarName, typeVarDef);
                }
                else
                {
                    quantified.Add((typeVarName, kind));
                }
            }
            return new TypeScheme(generalized, quantified);
        }

        private TypeInference Infer(ITerm term, out IType type)
        {
            Console.WriteLine($"Inferring: {term} with {this}");
            switch (term)
            {
                case TermVariable v:
                    var bound = this._state.Context.Last(c => (c is TermVariableBinding tvm) && tvm.Name == v.Name);
                    return this.Specialize((bound as TermVariableBinding).Type, out type);
                case LambdaAbstraction l:
                    var lambdaState = this
                        .Fresh(out var lambdaArgType)
                        .AddIntro(lambdaArgType, Kind.Value)
                        .AddBinding(l.Parameter, new TypeScheme(new TypeVariable(lambdaArgType)))
                        .Infer(l.Body, out var lambdaRetType)
                        .PopBinding(l.Parameter);
                    type = new ArrowType(new TypeVariable(lambdaArgType), lambdaRetType);
                    return lambdaState;
                case Application app:
                    var newState = this
                        .Infer(app.Func, out IType funType)
                        .Infer(app.Arg, out IType argType)
                        .Fresh(out string retType)
                        .AddIntro(retType, Kind.Value)
                        .Unify(funType, new ArrowType(argType, new TypeVariable(retType)));
                    type = new TypeVariable(retType);
                    return newState;
                case LetBinding let:
                    return this
                        .InferGeneralized(let.Bound, out var bodyType)
                        .AddBinding(let.Binder, bodyType)
                        .Infer(let.Expression, out type)
                        .PopBinding(let.Binder);
                case FloatLiteral lit:
                    type = new FloatType(new UnitIdentity());
                    return this;
                default:
                    throw new Exception("Inference applied to unsupported term.");
            }
        }

        private TypeInference Unify(IType left, IType right)
        {
            var unifyState = this._state.Push(new TypeConstraint(left, right));
            var result = unifyState.Unify(out var steps);
            Console.WriteLine($"Unify steps taken: {steps.Count}");
            foreach (var step in steps)
            {
                Console.WriteLine($"Unifying: {step}");
            }
            return new TypeInference(result);
        }

        public override string ToString() => this._state.ToString();
    }
}
