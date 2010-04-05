// --------------------------------------------------------------------------------------------------------------------
// <copyright file="KeyExpressionEvaluator.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation.
// </copyright>
// <summary>
//   Code to evaluate a predicate Expression and determine
//   a key range which contains all items matched by the predicate.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Microsoft.Isam.Esent.Collections.Generic
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    /// <summary>
    /// Contains methods to evaluate a predicate Expression and determine
    /// a key range which contains all items matched by the predicate.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    internal static class KeyExpressionEvaluator<TKey> where TKey : IComparable<TKey>
    {
        /// <summary>
        /// A MethodInfo describes TKey.CompareTo(TKey).
        /// </summary>
        private static readonly MethodInfo compareToMethod = typeof(TKey).GetMethod("CompareTo", new[] { typeof(TKey) });

        /// <summary>
        /// Evaluate a predicate Expression and determine a key range which
        /// contains all items matched by the predicate.
        /// </summary>
        /// <param name="expression">The expression to evaluate.</param>
        /// <param name="keyMemberName">The name of the parameter member that is the key.</param>
        /// <returns>
        /// A KeyRange that contains all items matched by the predicate. If no
        /// range can be determined the range will include all items.
        /// </returns>
        public static KeyRange<TKey> GetKeyRange(Expression expression, string keyMemberName)
        {
            if (null == expression)
            {
                throw new ArgumentNullException("expression");    
            }

            if (null == keyMemberName)
            {
                throw new ArgumentNullException("keyMemberName");
            }

            return GetKeyRangeOfSubtree(expression, keyMemberName);
        }

        /// <summary>
        /// Evaluate a predicate Expression and determine key range which
        /// contains all items matched by the predicate.
        /// </summary>
        /// <param name="expression">The expression to evaluate.</param>
        /// <param name="keyMemberName">The name of the parameter member that is the key.</param>
        /// <returns>
        /// A KeyRange containing all items matched by the predicate. If no
        /// range can be determined the ranges will include all items.
        /// </returns>
        private static KeyRange<TKey> GetKeyRangeOfSubtree(Expression expression, string keyMemberName)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.AndAlso:
                {
                    // Intersect the left and right parts
                    var binaryExpression = (BinaryExpression) expression;
                    return GetKeyRangeOfSubtree(binaryExpression.Left, keyMemberName)
                           & GetKeyRangeOfSubtree(binaryExpression.Right, keyMemberName);
                }

                case ExpressionType.OrElse:
                {
                    // Union the left and right parts
                    var binaryExpression = (BinaryExpression) expression;
                    return GetKeyRangeOfSubtree(binaryExpression.Left, keyMemberName)
                           | GetKeyRangeOfSubtree(binaryExpression.Right, keyMemberName);
                }

                case ExpressionType.Not:
                {
                    var unaryExpression = (UnaryExpression)expression;
                    return GetNegationOf(unaryExpression.Operand, keyMemberName);
                }

                case ExpressionType.Call:
                {
                    if (typeof(TKey) == typeof(string))
                    {
                        MethodCallExpression methodCall = (MethodCallExpression)expression;
                        if (null != methodCall.Object
                            && IsKeyAccess(methodCall.Object, keyMemberName))
                        {
                            TKey value;

                            // String.Equals
                            if (StringExpressionEvaluatorHelper.StringEqualsMethod == methodCall.Method
                                && ConstantExpressionEvaluator<TKey>.TryGetConstantExpression(methodCall.Arguments[0], out value))
                            {
                                return new KeyRange<TKey>(Key<TKey>.CreateKey(value, true), Key<TKey>.CreateKey(value, true));
                            }

                            // String.StartsWith
                            if (StringExpressionEvaluatorHelper.StringStartWithMethod == methodCall.Method
                                && ConstantExpressionEvaluator<TKey>.TryGetConstantExpression(methodCall.Arguments[0], out value))
                            {
                                // Lower range is just the string, upper range is the prefix
                                return new KeyRange<TKey>(Key<TKey>.CreateKey(value, true), Key<TKey>.CreatePrefixKey(value));
                            }
                        }
                    }

                    break;
                }

                case ExpressionType.Equal:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                {
                    // Return a range
                    var binaryExpression = (BinaryExpression) expression;
                    TKey value;
                    ExpressionType expressionType;
                    if (IsConstantComparison(binaryExpression, keyMemberName, out value, out expressionType))
                    {
                        switch (expressionType)
                        {
                            case ExpressionType.Equal:
                                var key = Key<TKey>.CreateKey(value, true);
                                return new KeyRange<TKey>(key, key);
                            case ExpressionType.LessThan:
                                return new KeyRange<TKey>(null, Key<TKey>.CreateKey(value, false));
                            case ExpressionType.LessThanOrEqual:
                                return new KeyRange<TKey>(null, Key<TKey>.CreateKey(value, true));
                            case ExpressionType.GreaterThan:
                                return new KeyRange<TKey>(Key<TKey>.CreateKey(value, false), null);
                            case ExpressionType.GreaterThanOrEqual:
                                return new KeyRange<TKey>(Key<TKey>.CreateKey(value, true), null);
                            default:
                                throw new InvalidOperationException(expressionType.ToString());
                        }
                    }

                    break;
                }

                default:
                    break;
            }

            return KeyRange<TKey>.OpenRange;
        }

        /// <summary>
        /// Get the negation of the given expression.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="keyMemberName">The name of the parameter member that is the key.</param>
        /// <returns>The negation of the given range.</returns>
        private static KeyRange<TKey> GetNegationOf(Expression expression, string keyMemberName)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Not:
                {
                    // Negation of a not simply means evaluating the condition
                    UnaryExpression unaryExpression = (UnaryExpression)expression;
                    return GetKeyRangeOfSubtree(unaryExpression.Operand, keyMemberName);
                }

                case ExpressionType.AndAlso:
                {
                    // DeMorgan's Law: !(A && B) -> !A || !B
                    BinaryExpression binaryExpression = (BinaryExpression)expression;
                    return GetNegationOf(binaryExpression.Left, keyMemberName) | GetNegationOf(binaryExpression.Right, keyMemberName);
                }

                case ExpressionType.OrElse:
                {
                    // DeMorgan's Law: !(A || B) -> !A && !B
                    BinaryExpression binaryExpression = (BinaryExpression)expression;
                    return GetNegationOf(binaryExpression.Left, keyMemberName) & GetNegationOf(binaryExpression.Right, keyMemberName);
                }

                case ExpressionType.Equal:
                {
                    return KeyRange<TKey>.OpenRange;
                }

                case ExpressionType.NotEqual:
                {
                    BinaryExpression binaryExpression = (BinaryExpression)expression;
                    return GetKeyRangeOfSubtree(
                        Expression.Equal(binaryExpression.Left, binaryExpression.Right), keyMemberName);
                }

                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                    return GetKeyRangeOfSubtree(expression, keyMemberName).Invert();
                default:
                    break;
            }

            return KeyRange<TKey>.OpenRange;
        }

        /// <summary>
        /// Calculate the union of a set of key ranges.
        /// </summary>
        /// <param name="ranges">The key ranges to union.</param>
        /// <returns>A union of all the ranges.</returns>
        private static KeyRange<TKey> UnionRanges(IEnumerable<KeyRange<TKey>> ranges)
        {
            return ranges.Aggregate(ranges.First(), (a, b) => a | b);
        }

        /// <summary>
        /// Calculate the intersection of a set of key ranges.
        /// </summary>
        /// <param name="ranges">The key ranges to union.</param>
        /// <returns>A union of all the ranges.</returns>
        private static KeyRange<TKey> IntersectRanges(IEnumerable<KeyRange<TKey>> ranges)
        {
            return ranges.Aggregate(ranges.First(), (a, b) => a & b);
        }

        /// <summary>
        /// Determine if the current binary expression involves the Key of the parameter
        /// and a constant value.
        /// </summary>
        /// <param name="expression">The expression to evaluate.</param>
        /// <param name="keyMemberName">The name of the parameter member that is the key.</param>
        /// <param name="value">Returns the value being compared to the key.</param>
        /// <param name="expressionType">Returns the type of the expression.</param>
        /// <returns>
        /// True if the expression involves the key of the parameter and a constant value.
        /// </returns>
        private static bool IsConstantComparison(BinaryExpression expression, string keyMemberName, out TKey value, out ExpressionType expressionType)
        {
            // Look for expression of the form x.Key [comparison] value
            //   e.g. x.Key < 0 or x.Key > (3 + 7)
            if (IsSimpleComparisonExpression(expression, keyMemberName, out value, out expressionType))
            {
                return true;
            }

            // Look for expressions of the form x.Key.CompareTo(value) [comparison] 0
            //   e.g. x.Key.CompareTo("foo") <= 0 or 0 > x.Key.CompareTo(5.67)
            // TKey implements IComparable<TKey> so we should expect this on all key types.
            if (IsCompareToExpression(expression, keyMemberName, out value, out expressionType))
            {
                return true;
            }

            // For string keys look for expressions of the form String.Compare(Key, value) [comparison] 0
            //   e.g. String.Compare(Key, "foo") < 0 or 0 > String.Compare("bar", Key)
            if (typeof(string) == typeof(TKey)
                && IsStringComparisonExpression(expression, keyMemberName, out value, out expressionType))
            {
                return true;
            }

            value = default(TKey);
            expressionType = default(ExpressionType);
            return false;
        }

        /// <summary>
        /// Determine if the binary expression is comparing the key value against a constant.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="keyMemberName">The name of the parameter key.</param>
        /// <param name="value">Returns the constant being compared against.</param>
        /// <param name="expressionType">Returns the type of the comparison.</param>
        /// <returns>True if this expression is comparing the key value against a constant.</returns>
        private static bool IsSimpleComparisonExpression(BinaryExpression expression, string keyMemberName, out TKey value, out ExpressionType expressionType)
        {
            if (IsKeyAccess(expression.Left, keyMemberName)
                && ConstantExpressionEvaluator<TKey>.TryGetConstantExpression(expression.Right, out value))
            {
                expressionType = expression.NodeType;
                return true;
            }

            if (IsKeyAccess(expression.Right, keyMemberName)
                && ConstantExpressionEvaluator<TKey>.TryGetConstantExpression(expression.Left, out value))
            {
                // The access is on the right so we have to switch the comparison type
                expressionType = GetReverseExpressionType(expression.NodeType);
                return true;
            }

            expressionType = ExpressionType.Equal;
            value = default(TKey);
            return false;
        }

        /// <summary>
        /// Determine if the binary expression is comparing the key value against a constant
        /// using CompareTo.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="keyMemberName">The name of the parameter key.</param>
        /// <param name="value">Returns the constant being compared against.</param>
        /// <param name="expressionType">Returns the type of the comparison.</param>
        /// <returns>True if this expression is comparing the key value against a constant.</returns>
        private static bool IsCompareToExpression(BinaryExpression expression, string keyMemberName, out TKey value, out ExpressionType expressionType)
        {
            // CompareTo is only guaranteed to return <0, 0 or >0 so allowing for
            // comparisons with values other than 0 is complicated/subtle.
            // One way this could be expanded is by recognizing "< 1", and "> -1" as well.
            if (IsCompareTo(expression.Left, keyMemberName, out value))
            {
                int comparison;
                if (ConstantExpressionEvaluator<int>.TryGetConstantExpression(expression.Right, out comparison)
                    && 0 == comparison)
                {
                    expressionType = expression.NodeType;

                    return true;
                }
            }

            if (IsCompareTo(expression.Right, keyMemberName, out value))
            {
                int comparison;
                if (ConstantExpressionEvaluator<int>.TryGetConstantExpression(expression.Left, out comparison)
                    && 0 == comparison)
                {
                    expressionType = GetReverseExpressionType(expression.NodeType);

                    return true;
                }
            }

            expressionType = ExpressionType.Equal;
            value = default(TKey);
            return false;
        }

        /// <summary>
        /// Determine if the binary expression is comparing the key value against a string
        /// using the simplest (two-argument) form of String.Compare.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="keyMemberName">The name of the parameter key.</param>
        /// <param name="value">Returns the constant being compared against.</param>
        /// <param name="expressionType">Returns the type of the comparison.</param>
        /// <returns>True if this expression is comparing the key value against a constant string.</returns>
        private static bool IsStringComparisonExpression(BinaryExpression expression, string keyMemberName, out TKey value, out ExpressionType expressionType)
        {
            Debug.Assert(typeof(string) == typeof(TKey), "This method should only be called for string keys");

            // CompareTo is only guaranteed to return <0, 0 or >0 so allowing for
            // comparisons with values other than 0 is complicated/subtle.
            // One way this could be expanded is by recognizing "< 1", and "> -1" as well.
            //
            // This code is tricky because there are 4 possibilities and we want
            // to turn them into a canonical form. In the first two cases we do
            // not reverse the sense of the comparison:
            //   1. String.Compare(Key, "m") < 0
            //   2. 0 < String.Compare("m", Key)
            // In the second two cases we do reverse the sense of the comparison:
            //   3. String.Compare("m", Key) > 0
            //   4. 0 > String.Compare(Key, "m")
            if (IsStringCompare(expression.Left, keyMemberName, out value))
            {
                int comparison;
                if (ConstantExpressionEvaluator<int>.TryGetConstantExpression(expression.Right, out comparison) && 0 == comparison)
                {
                    expressionType = expression.NodeType;
                    return true;
                }                
            }
            else if (IsStringCompareReversed(expression.Right, keyMemberName, out value))
            {
                int comparison;
                if (ConstantExpressionEvaluator<int>.TryGetConstantExpression(expression.Left, out comparison) && 0 == comparison)
                {
                    expressionType = expression.NodeType;
                    return true;
                }
            }
            else if (IsStringCompareReversed(expression.Left, keyMemberName, out value))
            {
                int comparison;
                if (ConstantExpressionEvaluator<int>.TryGetConstantExpression(expression.Right, out comparison) && 0 == comparison)
                {
                    expressionType = GetReverseExpressionType(expression.NodeType);
                    return true;
                }
            }
            else if (IsStringCompare(expression.Right, keyMemberName, out value))
            {
                int comparison;
                if (ConstantExpressionEvaluator<int>.TryGetConstantExpression(expression.Left, out comparison) && 0 == comparison)
                {
                    expressionType = GetReverseExpressionType(expression.NodeType);
                    return true;
                }
            }

            expressionType = ExpressionType.Equal;
            value = default(TKey);
            return false;
        }

        /// <summary>
        /// Reverse the type of the comparison. This is used when the key is on the right hand side
        /// of the comparison, so that 3 LT Key becomes Key GT 3.
        /// </summary>
        /// <param name="originalExpressionType">The original expression type.</param>
        /// <returns>The reverse of a comparison expression type or the original expression type.</returns>
        private static ExpressionType GetReverseExpressionType(ExpressionType originalExpressionType)
        {
            ExpressionType expressionType;
            switch (originalExpressionType)
            {
                case ExpressionType.LessThan:
                    expressionType = ExpressionType.GreaterThan;
                    break;
                case ExpressionType.LessThanOrEqual:
                    expressionType = ExpressionType.GreaterThanOrEqual;
                    break;
                case ExpressionType.GreaterThan:
                    expressionType = ExpressionType.LessThan;
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    expressionType = ExpressionType.LessThanOrEqual;
                    break;
                default:
                    expressionType = originalExpressionType;
                    break;
            }

            return expressionType;
        }

        /// <summary>
        /// Determine if the expression is accessing the key of the paramter of the expression.
        /// parameter.
        /// </summary>
        /// <param name="expression">The expression to evaluate.</param>
        /// <param name="keyMemberName">The name of the parameter member that is the key.</param>
        /// <returns>True if the expression is accessing the key of the parameter.</returns>
        private static bool IsKeyAccess(Expression expression, string keyMemberName)
        {
            if (expression.NodeType == ExpressionType.MemberAccess)
            {
                var member = (MemberExpression) expression;
                if (
                    null != member.Expression
                    && member.Expression.NodeType == ExpressionType.Parameter
                    && member.Member.Name == keyMemberName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determine if the expression is a call to [param].[member].CompareTo(value).
        /// </summary>
        /// <param name="expression">The expression to examine.</param>
        /// <param name="keyMemberName">The name of the key member.</param>
        /// <param name="value">Returns the string value being compared against.</param>
        /// <returns>
        /// True if the expression is a call to parameter.keyMember.CompareTo(value).
        /// </returns>
        private static bool IsCompareTo(Expression expression, string keyMemberName, out TKey value)
        {
            if (expression is MethodCallExpression)
            {
                MethodCallExpression methodCall = (MethodCallExpression)expression;
                if (methodCall.Method == compareToMethod
                    && null != methodCall.Object
                    && IsKeyAccess(methodCall.Object, keyMemberName))
                {
                    return ConstantExpressionEvaluator<TKey>.TryGetConstantExpression(methodCall.Arguments[0], out value);
                }
            }

            value = default(TKey);
            return false;
        }

        /// <summary>
        /// Determine if the expression is a call to String.Compare(key, value).
        /// </summary>
        /// <param name="expression">The expression to examine.</param>
        /// <param name="keyMemberName">The name of the key member.</param>
        /// <param name="value">Returns the string value being compared against.</param>
        /// <returns>
        /// True if the expression is a call to String.Compare(key, value).
        /// </returns>
        private static bool IsStringCompare(Expression expression, string keyMemberName, out TKey value)
        {
            if (expression is MethodCallExpression)
            {
                MethodCallExpression methodCall = (MethodCallExpression)expression;
                if (methodCall.Method == StringExpressionEvaluatorHelper.StringCompareMethod
                    && IsKeyAccess(methodCall.Arguments[0], keyMemberName))
                {
                    return ConstantExpressionEvaluator<TKey>.TryGetConstantExpression(methodCall.Arguments[1], out value);
                }
            }

            value = default(TKey);
            return false;
        }

        /// <summary>
        /// Determine if the expression is a call to String.Compare(value, key).
        /// </summary>
        /// <param name="expression">The expression to examine.</param>
        /// <param name="keyMemberName">The name of the key member.</param>
        /// <param name="value">Returns the string value being compared against.</param>
        /// <returns>
        /// True if the expression is a call to String.Compare(value, key).
        /// </returns>
        private static bool IsStringCompareReversed(Expression expression, string keyMemberName, out TKey value)
        {
            if (expression is MethodCallExpression)
            {
                MethodCallExpression methodCall = (MethodCallExpression)expression;
                if (methodCall.Method == StringExpressionEvaluatorHelper.StringCompareMethod
                    && IsKeyAccess(methodCall.Arguments[1], keyMemberName))
                {
                    return ConstantExpressionEvaluator<TKey>.TryGetConstantExpression(methodCall.Arguments[0], out value);
                }
            }

            value = default(TKey);
            return false;
        }
    }
}