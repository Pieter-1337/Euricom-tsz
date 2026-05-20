namespace Tsz.Infrastructure.Common.Pagination;

/// <summary>
/// See http://www.albahari.com/expressions for information and examples.
/// https://github.com/scottksmith95/LINQKit also contains a PredicateBuilder, but you need to call
/// AsExpandable on all of your IQueryable objects before using any LinqKit methods (otherwise:
/// exception on "Invoke").
/// Alternative 1: Universal PredicateBuilder - https://petemontgomery.wordpress.com/2011/02/10/a-universal-predicatebuilder/
/// Alternative 2: this one.
/// </summary>
public static class PredicateBuilder
{
    /// <summary>Predicate that evaluates to true. Starting point for an AND clause.</summary>
    public static Expression<Func<T, bool>> BaseAnd<T>() => _ => true;

    /// <summary>Predicate that evaluates to false. Starting point for an OR clause.</summary>
    public static Expression<Func<T, bool>> BaseOr<T>() => _ => false;

    /// <summary>Wraps the given lambda — useful for creating an explicitly typed starting predicate.</summary>
    public static Expression<Func<T, bool>> Create<T>(Expression<Func<T, bool>> predicate) => predicate;

    /// <summary>Combines two predicates with logical OR. The second predicate's parameter is rebound to the first's.</summary>
    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> expr1, Expression<Func<T, bool>> expr2)
    {
        var secondBody = expr2.Body.Replace(expr2.Parameters[0], expr1.Parameters[0]);
        return Expression.Lambda<Func<T, bool>>(Expression.OrElse(expr1.Body, secondBody), expr1.Parameters);
    }

    /// <summary>Combines two predicates with logical AND. The second predicate's parameter is rebound to the first's.</summary>
    public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> expr1, Expression<Func<T, bool>> expr2)
    {
        var secondBody = expr2.Body.Replace(expr2.Parameters[0], expr1.Parameters[0]);
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(expr1.Body, secondBody), expr1.Parameters);
    }

    /// <summary>Negates the given predicate.</summary>
    public static Expression<Func<T, bool>> Not<T>(this Expression<Func<T, bool>> expression) =>
        Expression.Lambda<Func<T, bool>>(Expression.Not(expression.Body), expression.Parameters);

    /// <summary>Returns true when the predicate is still the untouched <see cref="BaseAnd{T}"/> starting point — so it can be skipped when issuing a query.</summary>
    public static bool EqualsBaseAnd<T>(this Expression<Func<T, bool>> expression) =>
        expression.Body.ToString().Equals(BaseAnd<T>().Body.ToString());

    /// <summary>Returns true when the predicate is still the untouched <see cref="BaseOr{T}"/> starting point — so it can be skipped when issuing a query.</summary>
    public static bool EqualsBaseOr<T>(this Expression<Func<T, bool>> expression) =>
        expression.Body.ToString().Equals(BaseOr<T>().Body.ToString());

    internal static Expression Replace(this Expression expression, Expression searchEx, Expression replaceEx) =>
        new ReplaceVisitor(searchEx, replaceEx).Visit(expression)!;

    internal sealed class ReplaceVisitor(Expression from, Expression to) : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node) =>
            node == from ? to : base.Visit(node);
    }
}
