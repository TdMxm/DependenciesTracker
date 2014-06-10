﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DependenciesTracker.Interfaces;
using JetBrains.Annotations;


namespace DependenciesTracker
{
    public sealed class DependenciesMap<T> : IDependenciesMap<T>
    {
        [NotNull]
        private readonly IList<PathItem<T>> _mapItems = new List<PathItem<T>>();

        [NotNull]
        private readonly ReadOnlyCollection<PathItem<T>> _readOnlyMapItems;

        [NotNull]
        internal ReadOnlyCollection<PathItem<T>> MapItems
        {
            get { return _readOnlyMapItems; }
        }

        private static readonly MethodInfo _anyElementMethodInfo;

        static DependenciesMap()
        {
            Expression<Func<ICollection<object>, object>> anyElementExpression = c => c.AnyElement();
            _anyElementMethodInfo = ((MethodCallExpression) anyElementExpression.Body).Method.GetGenericMethodDefinition();
        }

        public DependenciesMap()
        {
            _readOnlyMapItems = new ReadOnlyCollection<PathItem<T>>(_mapItems);
        }

        [NotNull]
        public IDependenciesMap<T> AddMap<U>([NotNull] Action<T, U> setter, [NotNull] Func<T, U> calculator, [NotNull] params Expression<Func<T, object>>[] dependencyPaths)
        {
            if (setter == null)
                throw new ArgumentNullException("setter");
            if (calculator == null)
                throw new ArgumentNullException("calculator");

            foreach (var builtPath in BuildPaths(dependencyPaths, o => setter(o, calculator(o))))
                _mapItems.Add(builtPath);

            return this;
        }

        [NotNull]
        public IDependenciesMap<T> AddMap<U>([NotNull] Expression<Func<T, U>> dependentProperty, [NotNull] Func<T, U> calculator, [NotNull] params Expression<Func<T, object>>[] dependencyPaths)
        {
            if (dependentProperty == null)
                throw new ArgumentNullException("dependentProperty");

            return AddMap(BuildSetter(dependentProperty), calculator, dependencyPaths);
        }

        [NotNull]
        private static Action<T, U> BuildSetter<U>([NotNull] Expression<Func<T, U>> dependentProperty)
        {
            var memberExpression = (MemberExpression)dependentProperty.Body;
            var objectParameter = Expression.Parameter(typeof(T), "obj");
            var assignParameter = Expression.Parameter(typeof(U), "val");
            var property = Expression.PropertyOrField(objectParameter, memberExpression.Member.Name);
            var lambda = Expression.Lambda<Action<T, U>>(Expression.Assign(property, assignParameter), objectParameter, assignParameter);
            Debug.WriteLine(lambda);
            return lambda.Compile();
        }


        [NotNull]
        private static IEnumerable<PathItem<T>> BuildPaths([NotNull] IEnumerable<Expression<Func<T, object>>> dependencyPaths,
            [NotNull] Action<T> calculateAndSet)
        {
            if (calculateAndSet == null)
                throw new ArgumentNullException("calculateAndSet");

            return dependencyPaths.Select(pathExpression => BuildPath(pathExpression, calculateAndSet)).ToList();
        }

        [NotNull]
        private static PathItem<T> BuildPath([NotNull] Expression<Func<T, object>> pathExpession, Action<T> calculateAndSet)
        {
            var convertExpression = pathExpession.Body as UnaryExpression;
            if (convertExpression != null && convertExpression.NodeType != ExpressionType.Convert)
                throw new InvalidOperationException("unary expression is not a convert expression");

            var memberOrMethodCallExpression = convertExpression != null ? convertExpression.Operand : pathExpession.Body;

            PathItem<T> rootPathItem = null;

            var lastChainItemIsCollection = false;

            while (memberOrMethodCallExpression != null)
            {
                var methodCall = memberOrMethodCallExpression as MethodCallExpression;
                if (methodCall != null)
                {
                    if (!methodCall.Method.GetGenericMethodDefinition().Equals(_anyElementMethodInfo))
                        throw new NotSupportedException(string.Format("Call of method {0} is not supported", methodCall.Method));

                    memberOrMethodCallExpression = methodCall.Arguments.Single();
                    lastChainItemIsCollection = true;
                    continue;
                }

                var memberExpression = memberOrMethodCallExpression as MemberExpression;
                if (memberExpression == null)
                    throw new InvalidOperationException( string.Format("Actual expression is {0}, but member expression is expected", memberOrMethodCallExpression));

                var property = memberExpression.Member;
                var compiledGetter = BuildGetter(memberExpression.Expression.Type, property.Name);

                rootPathItem = new PathItem<T>(compiledGetter, property.Name, lastChainItemIsCollection, rootPathItem, rootPathItem == null ? calculateAndSet : null);

                lastChainItemIsCollection = false;

                if (memberExpression.Expression == null || memberExpression.Expression is ParameterExpression)
                    break;

                if (!(memberExpression.Expression is MemberExpression) && !(memberExpression.Expression is MethodCallExpression))
                    throw new InvalidOperationException(string.Format("The expression {0} should be either member or method call expression", memberExpression.Expression));

                memberOrMethodCallExpression = memberExpression.Expression;
            }

            Debug.Assert(rootPathItem != null);

            return rootPathItem;
        }

        [NotNull]
        private static Func<object, object> BuildGetter([NotNull] Type parameterType, [NotNull] string propertyName)
        {
            var parameter = Expression.Parameter(typeof(object), "obj");
            var convertedParameter = Expression.Convert(parameter, parameterType);
            var propertyGetter = Expression.Property(convertedParameter, propertyName);
            Debug.WriteLine(propertyGetter);

            var lambdaExpression = Expression.Lambda<Func<object, object>>(Expression.Convert(propertyGetter, typeof(object)), parameter);
            Debug.WriteLine(lambdaExpression);
            return lambdaExpression.Compile();

        }

        public IDisposable StartTracking([NotNull] T trackedObject)
        {
            return new DependenciesTracker<T>(this, trackedObject);
        }
    }
}