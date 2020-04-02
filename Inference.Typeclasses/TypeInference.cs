using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Inference.Common;

namespace Inference.Typeclasses
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

        public static QualifiedType Infer(InferenceState initial, ITerm term)
        {
            var inference = new TypeInference(initial).Infer(term, out var result);
            Console.WriteLine($"Final: {inference}");
            var normalized = inference._state.Context.Normalize().Apply(result);
            return new QualifiedType(normalized.Context.Reduce(inference._state.Context), normalized.Head);
        }

        private TypeInference Fresh(out string name)
        {
            var newStream = this._state.Fresh.Next("t", out name);
            return new TypeInference(newStream, this._state.Context);
        }

        private TypeInference AddIntro(string name, IKind kind) =>
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

        private TypeInference Specialize(TypeScheme scheme, out QualifiedType specialized)
        {
            var state = this;
            specialized = scheme.Body;
            foreach (var (name, kind) in scheme.Quantified)
            {
                state = state
                    .Fresh(out var tname)
                    .AddIntro(tname, kind);
                specialized = specialized.Substitute(name, new TypeVariable(tname, kind));
            }
            return state;
        }

        private TypeInference InferGeneralized(ITerm term, out TypeScheme generalized, out IImmutableList<Predicate> deferred)
        {
            var state = this
                .AddMark()
                .Infer(term, out var ungeneralized)
                .SkimContext(out var skimmed);
            var (ds, rs) = ungeneralized.Context.SplitPredicates(state._state.Context, skimmed.Select(s => s.name));
            deferred = ds;
            generalized = this.MakeScheme(skimmed, new QualifiedType(rs, ungeneralized.Head));
            return state;
        }

        private TypeInference SkimContext(out IImmutableList<(string name, IKind kind, IType? definition)> skimmed)
        {
            skimmed = ImmutableList<(string name, IKind kind, IType? definition)>.Empty;
            var last = this._state.Context.Last();
            var newContext = this._state.Context.RemoveAt(this._state.Context.Count - 1);
            while (!(last is LocalityMarker))
            {
                skimmed = last switch
                {
                    TypeVariableIntro intro => skimmed.Add((intro.Name, intro.Kind, null)),
                    TypeVariableDefinition def => skimmed.Add((def.Name, def.Definition.Kind, def.Definition)),
                    _ => throw new Exception($"Unexpected entry in skimmed context: {last}"),
                };
                last = newContext.Last();
                newContext = newContext.RemoveAt(newContext.Count - 1);
            }
            return new TypeInference(this._state.Fresh, newContext);
        }

        private TypeScheme MakeScheme(IReadOnlyList<(string name, IKind kind, IType? definition)> skimmedFromContext, QualifiedType ungeneralized)
        {
            var generalized = ungeneralized;
            var quantified = new List<(string, IKind)>();
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

        private TypeInference Infer(ITerm term, out QualifiedType type)
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
                        .AddIntro(lambdaArgType, new DataKind())
                        .AddBinding(l.Parameter, new TypeScheme(new TypeVariable(lambdaArgType, new DataKind())))
                        .Infer(l.Body, out var lambdaRetType)
                        .PopBinding(l.Parameter);
                    type = new QualifiedType(lambdaRetType.Context, PrimType.Fun(new TypeVariable(lambdaArgType, new DataKind()), lambdaRetType.Head));
                    return lambdaState;
                case Application app:
                    var newState = this
                        .Infer(app.Func, out QualifiedType funType)
                        .Infer(app.Arg, out QualifiedType argType)
                        .Fresh(out string retType)
                        .AddIntro(retType, new DataKind())
                        .Unify(funType.Head, PrimType.Fun(argType.Head, new TypeVariable(retType, new DataKind())));
                    type = new QualifiedType(funType.Context.AddRange(argType.Context), new TypeVariable(retType, new DataKind()));
                    return newState;
                case LetBinding let:
                    var state = this
                        .InferGeneralized(let.Bound, out var bodyType, out var deferredPreds)
                        .AddBinding(let.Binder, bodyType)
                        .Infer(let.Expression, out var exprType)
                        .PopBinding(let.Binder);
                    type = new QualifiedType(deferredPreds.AddRange(exprType.Context), exprType.Head);
                    return state;
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
