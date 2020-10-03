using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Inference.Common;

namespace Inference.Dots
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

        public static IImmutableList<Predicate> Apply(this IImmutableDictionary<string, IType> subst, Predicate target)
        {
            var result = ImmutableList.Create(target);
            foreach (var kv in subst)
            {
                result = result.SelectMany(p => p.Substitute(kv.Key, kv.Value)).ToImmutableList();
            }
            return result;
        }

        public static IImmutableList<IType> Apply(this IImmutableDictionary<string, IType> subst, IImmutableList<IType> targets) =>
            targets.Select(t => subst.Apply(t)).ToImmutableList();

        public static IImmutableList<Predicate> Apply(this IImmutableDictionary<string, IType> subst, IImmutableList<Predicate> targets) =>
            targets.SelectMany(t => subst.Apply(t)).ToImmutableList();

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

        public static IImmutableDictionary<string, IType> MergeWithSeqVars(this IImmutableDictionary<string, IType> left, IImmutableDictionary<string, IType> right, IEnumerable<string> seqVars)
        {
            // check to see that substitutions have the same value for each key
            // the special case here is to form sequence substitutions for the given list of sequence variables
            // AddRange does the same check but this gives a more informative error message
            var merged = ImmutableDictionary<string, IType>.Empty;
            foreach (var lkv in left)
            {
                if (seqVars.Contains(lkv.Key))
                {
                    if (right.TryGetValue(lkv.Key, out var rv))
                    {
                        if (lkv.Value is TypeSequence ts)
                        {
                            merged = merged.Add(lkv.Key, new TypeSequence(ts.Types.Add(rv)));
                        }
                        else
                        {
                            merged = merged.Add(lkv.Key, new TypeSequence(ImmutableList.Create(lkv.Value, rv)));
                        }
                    }
                    else
                    {
                        merged = merged.Add(lkv.Key, lkv.Value);
                    }
                }
                else if (right.TryGetValue(lkv.Key, out var rv) && lkv.Value != rv)
                {
                    throw new Exception($"Cannot merge substitutions, values at variable '{lkv.Key}' are not equal: {lkv.Value}, {rv}");
                }
                else
                {
                    merged = merged.Add(lkv.Key, lkv.Value);
                }
            }

            return merged.AddRange(right.Where(r => !merged.ContainsKey(r.Key)));
        }

        public static IImmutableDictionary<string, IType> CapWithDotted(this IImmutableDictionary<string, IType> subst, IImmutableDictionary<string, IType> dotted)
        {
            /*var merged = subst;
            foreach (var kvp in dotted)
            {
                if (merged.ContainsKey(kvp.Key))
                {
                    if (merged[kvp.Key] is TypeSequence)
                    {
                        merged = merged.SetItem(kvp.Key, new TypeSequence((merged[kvp.Key] as TypeSequence).Types, kvp.Value));
                    }
                    else
                    {
                        merged = merged.SetItem(kvp.Key, new TypeSequence(ImmutableList.Create(merged[kvp.Key]), kvp.Value));
                    }
                }
                else
                {
                    merged = merged.Add(kvp.Key, new TypeSequence(ImmutableList<IType>.Empty, kvp.Value));
                }
            }
            return merged;*/
            throw new NotImplementedException();
        }
    }
}
