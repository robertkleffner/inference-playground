using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Inference.Typeclasses
{
    public static class SubstitutionExt
    {
        public static IType Apply(this IImmutableDictionary<string, IType> subst, IType target)
        {
            var result = target;
            foreach (var kv in subst)
            {
                result = result.Substitute(kv.Key, kv.Value);
            }
            return result;
        }

        public static Predicate Apply(this IImmutableDictionary<string, IType> subst, Predicate target)
        {
            var result = target;
            foreach (var kv in subst)
            {
                result = result.Substitute(kv.Key, kv.Value);
            }
            return result;
        }

        public static IImmutableList<IType> Apply(this IImmutableDictionary<string, IType> subst, IImmutableList<IType> targets) =>
            targets.Select(t => subst.Apply(t)).ToImmutableList();

        public static IImmutableList<Predicate> Apply(this IImmutableDictionary<string, IType> subst, IImmutableList<Predicate> targets) =>
            targets.Select(t => subst.Apply(t)).ToImmutableList();

        public static IImmutableDictionary<string, IType> Compose(this IImmutableDictionary<string, IType> left, IImmutableDictionary<string, IType> right) =>
            right.Select(kv => new KeyValuePair<string, IType>(kv.Key, left.Apply(kv.Value)))
                .ToImmutableDictionary()
                .SetItems(left);

        public static IImmutableDictionary<string, IType> Merge(this IImmutableDictionary<string, IType> left, IImmutableDictionary<string, IType> right)
        {
            // check to see that substitutions have the same value for each key
            // AddRange does the same check but this gives a more informative error message
            foreach (var lkv in left)
            {
                if (right.TryGetValue(lkv.Key, out var rv) && lkv.Value != rv)
                {
                    throw new Exception($"Cannot merge substitutions, values at variable '{lkv.Key}' are not equal: {lkv.Value}, {rv}");
                }
            }

            return left.AddRange(right);
        }
    }
}
