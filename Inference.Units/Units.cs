using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;

using Inference.Common;

namespace Inference.Units
{
    /// <summary>
    /// Represents a Unit as an Abelian group, composed of constant values and variables which can each have a signed integer exponent.
    /// The implementation uses dictionaries to implement a form of signed multiset, where an element in the set can have more than one occurence
    /// (represented by a positive exponent) or even negative occurences (represented by a negative exponent). If an element has exactly zero
    /// occurences, it is removed from the dictionary for efficiency.
    /// </summary>
    public class Unit : IHasVariables<string>, ICanSubstitute<string, Unit>
    {
        /// <summary>
        /// The set of variables in the unit equation, mapped to their exponents.
        /// </summary>
        public IImmutableDictionary<string, int> Variables { get; }
        /// <summary>
        /// The set of constants in the unit equation, mapped to their exponents.
        /// </summary>
        public IImmutableDictionary<string, int> Constants { get; }

        /// <summary>
        /// Create a unit from a list of known exponeniated variables and constants.
        /// </summary>
        public Unit(IReadOnlyDictionary<string, int> variables, IReadOnlyDictionary<string, int> constants)
        {
            this.Variables = variables.ToImmutableDictionary();
            this.Constants = constants.ToImmutableDictionary();
        }

        /// <summary>
        /// Create a unit equation with a single sole variable (exponent 1) and no constants.
        /// </summary>
        public Unit(string soleVariable)
        {
            this.Variables = ImmutableDictionary.CreateRange(new[] { new KeyValuePair<string, int>(soleVariable, 1) });
            this.Constants = ImmutableDictionary<string, int>.Empty;
        }

        /// <summary>
        /// Create an empty unit equation, equal to the identity (commonly written '1').
        /// </summary>
        public Unit()
        {
            this.Variables = ImmutableDictionary<string, int>.Empty;
            this.Constants = ImmutableDictionary<string, int>.Empty;
        }

        public bool IsIdentity() =>
            this.Variables.Count == 0 && this.Constants.Count == 0;

        public bool IsConstant() =>
            this.Variables.Count == 0;

        public int ExponentOf(string variableName) =>
            this.Variables.GetValueOrDefault(variableName, 0);

        /// <summary>
        /// Determines whether all exponents in the equation are integer multiples of the given divisor.
        /// </summary>
        public bool DividesPowers(int divisor) =>
            this.Variables.Values.All(pow => pow % divisor == 0)
            && this.Constants.Values.All(pow => pow % divisor == 0);

        /// <summary>
        /// For a given variable, returns whether there is another variable in the equation that has a higher exponent.
        /// </summary>
        public bool NotMax(string variableName)
        {
            var examined = Math.Abs(this.ExponentOf(variableName));
            return this.Variables.Any(p => p.Key != variableName && Math.Abs(p.Value) >= examined);
        }

        /// <summary>
        /// Negates each exponent in the equation.
        /// </summary>
        public Unit Invert()
        {
            var newVariables = this.Variables.ToDictionary(p => p.Key, p => -p.Value);
            var newRigids = this.Constants.ToDictionary(p => p.Key, p => -p.Value);
            return new Unit(newVariables, newRigids);
        }

        /// <summary>
        /// Combine two Abelian unit equations. Values that appear in both equations have their exponents multiplied.
        /// </summary>
        public Unit Add(Unit other)
        {
            var newVariables = new[] { this.Variables, other.Variables }.SelectMany(d => d)
                .ToLookup(p => p.Key, p => p.Value)
                .ToDictionary(group => group.Key, group => group.Sum())
                .Where(p => p.Value != 0)
                .ToDictionary(p => p.Key, p => p.Value);
            var newRigids = new[] { this.Constants, other.Constants }.SelectMany(d => d)
                .ToLookup(p => p.Key, p => p.Value)
                .ToDictionary(group => group.Key, group => group.Sum())
                .Where(p => p.Value != 0)
                .ToDictionary(p => p.Key, p => p.Value);
            return new Unit(newVariables, newRigids);
        }

        /// <summary>
        /// Removes the given equation from this Abelian unit equation. Equivalent to `this.Add(other.Invert())`.
        /// </summary>
        public Unit Subtract(Unit other) => this.Add(other.Invert());

        /// <summary>
        /// Multiplies each exponent in the unit equation by the given factor.
        /// </summary>
        public Unit Scale(int factor)
        {
            var newVariables = this.Variables.ToDictionary(p => p.Key, p => p.Value * factor);
            var newRigids = this.Constants.ToDictionary(p => p.Key, p => p.Value * factor);
            return new Unit(newVariables, newRigids);
        }

        /// <summary>
        /// Divides each exponent in the unit equation by the given factor.
        /// </summary>
        public Unit Divide(int factor)
        {
            var newVariables = this.Variables.ToDictionary(p => p.Key, p => p.Value / factor);
            var newRigids = this.Constants.ToDictionary(p => p.Key, p => p.Value / factor);
            return new Unit(newVariables, newRigids);
        }

        /// <summary>
        /// Removes the specified variable from the unit, and divides all other powers by the removed variable's power.
        /// </summary>
        public Unit Pivot(string variable)
        {
            var pivotPower = this.ExponentOf(variable);
            return this
                .Subtract(new Unit(variable).Scale(pivotPower))
                .Divide(pivotPower)
                .Invert();
        }

        public IImmutableSet<string> FreeVariables() => this.Variables.Keys.ToImmutableHashSet();

        /// <summary>
        /// Substitutes the given unit for the specified variable, applying the variable's power to the substituted unit.
        /// </summary>
        public Unit Substitute(string name, Unit other) =>
            other.Subtract(new Unit(name))
                .Scale(this.ExponentOf(name))
                .Add(this);

        public IType ToType()
        {
            static IType ElemToVar(KeyValuePair<string, int> elem) => new UnitPower(new TypeVariable(elem.Key), elem.Value);
            static IType ElemToPrim(KeyValuePair<string, int> elem) => new UnitPower(new PrimitiveUnit(elem.Key), elem.Value);

            var varFirst = this.Variables.Any() ? ElemToVar(this.Variables.First()) : new UnitIdentity();
            var rigidFirst = this.Constants.Any() ? ElemToPrim(this.Constants.First()) : new UnitIdentity();
            return (this.Variables.Any(), this.Constants.Any()) switch
            {
                (false, false) => new UnitIdentity(),
                (false, true) => this.Constants.Skip(1).Aggregate(rigidFirst, (t, u) => new UnitMultiply(t, ElemToPrim(u))),
                (true, false) => this.Variables.Skip(1).Aggregate(varFirst, (t, u) => new UnitMultiply(t, ElemToVar(u))),
                _ => new UnitMultiply(
                    this.Variables.Skip(1).Aggregate(varFirst, (t, u) => new UnitMultiply(t, ElemToVar(u))),
                    this.Constants.Skip(1).Aggregate(rigidFirst, (t, u) => new UnitMultiply(t, ElemToPrim(u)))),
            };
        }

        public override string ToString()
        {
            if (this.IsIdentity()) { return "1"; }

            var prettyElems = new[] { this.Variables, this.Constants }.SelectMany(d => d).Select(p => $"({p.Key}^{p.Value})");
            return string.Join("*", prettyElems);
        }
    }
}
