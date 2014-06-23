﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using DependenciesTracker.Interfaces;
using Xunit;
using Xunit.Extensions;

namespace DependenciesTracker.Tests.PathBuilding
{
    public interface IPathBuildingTestClass
    {
        int IntProperty { get; set; }
    }

    public class PathBuildingTestClass : IPathBuildingTestClass
    {
        public int IntProperty { get; set; }
        public int IntField;
        public readonly int IntFieldReadOnly = -1;

        public int DependentProperty { get; set; }

        public IList<string> Strings { get; set; }
        public List<List<string>> StringLists { get; set; }

        public List<int> Ints { get; set; }
        public List<List<int>> IntLists { get; set; }

        public string StringProperty { get; set; }
        public string StringField;
        public readonly string StringFieldReadOnly = string.Empty;

        public PathBuildingInnerTestClass InnerProperty { get; set; }
        public PathBuildingInnerTestClass InnerField;

        public readonly PathBuildingInnerTestClass InnerFieldReadOnly = new PathBuildingInnerTestClass();

        public PathBuildingTestClass Child { get; set; }
    }

    public class PathBuildingCollectionTestClass<T> : ObservableCollection<T>
    {
        public int IntProperty { get; set; }
    }

    public class PathBuildingInnerTestClass
    {
        public int IntProperty { get; set; }
        public int IntField;
        public int IntFieldReadOnly = -1;
        public string StringProperty { get; set; }
        public string StringField;
        public readonly string StringFieldReadOnly = string.Empty;
    }

    public static class IntegerExtension
    {
        public static string ExtensionCall(this int i)
        {
            throw new NotSupportedException("Call of this method is not supported");
        }
    }

    public static class CollectionExtensions
    {
        public static T EachElement<T>(this ICollection<T> collection)
        {
            throw new NotSupportedException("Call of this method is not supported");
        }
    }

    public class PathBuildingTestClassWithPrivatePropertiesAndFields
    {
        private int _priceProperty { get; set; }
        private int _quantityProperty { get; set; }

        private int _costProperty { get; set; }

        private int _priceField;
        private int _quantityField;

        private int _costField;

        public int GetCostProperty() { return _costProperty; }
        public int GetCostField() { return _costField; }

        public static readonly DependenciesMap<PathBuildingTestClassWithPrivatePropertiesAndFields> PropertiesDependenciesMap;
        public static readonly DependenciesMap<PathBuildingTestClassWithPrivatePropertiesAndFields> FieldsDependenciesMap;

        static PathBuildingTestClassWithPrivatePropertiesAndFields()
        {
            PropertiesDependenciesMap = new DependenciesMap<PathBuildingTestClassWithPrivatePropertiesAndFields>();
            PropertiesDependenciesMap.AddMap(o => o._costProperty, o => o._priceProperty * o._quantityProperty, o => o._priceProperty,
                            o => o._quantityProperty);

            FieldsDependenciesMap = new DependenciesMap<PathBuildingTestClassWithPrivatePropertiesAndFields>();
            FieldsDependenciesMap.AddMap(o => o._costField, o => o._priceField * o._quantityField, o => o._priceField,
                            o => o._quantityField);
        }

        private PathBuildingTestClassWithPrivatePropertiesAndFields() { }

        public static PathBuildingTestClassWithPrivatePropertiesAndFields CreateWithPrivateProperties(int price, int quantity)
        {
            var obj = new PathBuildingTestClassWithPrivatePropertiesAndFields
            {
                _priceProperty = price,
                _quantityProperty = quantity
            };

            return obj;
        }


        public static PathBuildingTestClassWithPrivatePropertiesAndFields CreateWithPrivateFields(int price, int quantity)
        {
            var obj = new PathBuildingTestClassWithPrivatePropertiesAndFields
            {
                _priceField = price,
                _quantityField = quantity
            };

            return obj;
        }
    }

    public class PathBuildingTests
    {
        public static IEnumerable<Expression<Func<PathBuildingTestClass, object>>[]> AddDependency_RootOnlyPath_NotSupported_TestData
        {
            get
            {
                //With convert
                yield return new Expression<Func<PathBuildingTestClass, object>>[] { o => (object)o };
                //With no convert
                yield return new Expression<Func<PathBuildingTestClass, object>>[] { o => o };
            }
        }

        public static IEnumerable<Expression<Func<PathBuildingTestClass, object>>[]> AddDependency_ExternalMethodCallsInPath_NotSupported_TestData
        {
            get
            {
                //Instance method of simple property
                yield return new Expression<Func<PathBuildingTestClass, object>>[] { o => o.IntProperty.ToString().Length };
                //Extension method of simple property
                yield return new Expression<Func<PathBuildingTestClass, object>>[] { o => o.IntProperty.ExtensionCall().Length };
                //Instance method of collection property
                yield return new Expression<Func<PathBuildingTestClass, object>>[] { o => o.Strings.ToArray().Length };
                //Extension method of collection property
                yield return new Expression<Func<PathBuildingTestClass, object>>[] { o => o.Strings.First().Length };
                //Extension method of collection property with the same name of class and method as DependenciesTracker.CollectionExtensions.EachElement<T>
                yield return new Expression<Func<PathBuildingTestClass, object>>[] { o => o.Strings.EachElement().Length };
            }
        }

        public static IEnumerable<Expression<Func<PathBuildingTestClass, object>>[]> AddDependency_ConvertsInsideThePath_NotSupported_TestData
        {
            get
            {
                //Only single convert to "object" is allowed in the end of path
                yield return new Expression<Func<PathBuildingTestClass, object>>[] { o => (decimal)o.DependentProperty };
                //Only single convert to "object" is allowed in the end of path
                yield return new Expression<Func<PathBuildingTestClass, object>>[] { o => (object)(decimal)o.DependentProperty };
                //Only single convert to "object" is allowed in the end of path
                yield return new Expression<Func<PathBuildingTestClass, object>>[] { o => (IPathBuildingTestClass)o.Child };
                //Only single convert to "object" is allowed in the end of path
                yield return new Expression<Func<PathBuildingTestClass, object>>[] { o => (object)(IPathBuildingTestClass)o.Child };
                //No converts are allowed at the begginning of path (i.e. root object convertion isn't allowed)
                yield return new Expression<Func<PathBuildingTestClass, object>>[] { o => ((IPathBuildingTestClass)o).IntProperty };
                //No converts are allowed in the middle of path
                yield return new Expression<Func<PathBuildingTestClass, object>>[] { o => ((IPathBuildingTestClass)o.Child).IntProperty };
            }
        }

        [Theory]
        [PropertyData("AddDependency_RootOnlyPath_NotSupported_TestData")]
        [PropertyData("AddDependency_ExternalMethodCallsInPath_NotSupported_TestData")]
        [PropertyData("AddDependency_ConvertsInsideThePath_NotSupported_TestData")]
        public void AddDependency_NotSupportedPaths(Expression<Func<PathBuildingTestClass, object>> path)
        {
            var map = new DependenciesMap<PathBuildingTestClass>();

            Assert.Throws<NotSupportedException>(() =>
                map.AddMap(o => o.DependentProperty, o => -1, path));
        }

        public static IEnumerable<object[]> AddDependency_ConvertToObject_AllowedAtTheEndOfPath_TestData
        {
            get
            {
                //Explicit conversion of value type
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>) (o => (object) o.IntProperty),
                    new[] {"root", "IntProperty"}
                };

                //Implicit conversion of value type
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>) (o => o.IntProperty),
                    new[] {"root", "IntProperty"}
                };

                //Explicit conversion of reference type
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>) (o => (object) o.Child),
                    new[] {"root", "Child"}
                };

                //Explicit conversion of collection
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>) (o => (object) o.Strings),
                    new[] {"root", "Strings"}
                };

                //Explicit conversion of "EachElement" call on collection of reference type elements
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>) (o => (object) DependenciesTracker.CollectionExtensions.EachElement(o.Ints)),
                    new[] {"root", "Ints", "CollectionItem"}
                };

                //Implicit conversion of "EachElement" call on collection of value type elements
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>) (o => (object) DependenciesTracker.CollectionExtensions.EachElement(o.Ints)),
                    new[] {"root", "Ints", "CollectionItem"}
                };
            }
        }

        [Theory]
        [PropertyData("AddDependency_ConvertToObject_AllowedAtTheEndOfPath_TestData")]
        public void AddDependency_ConvertToObject_AllowedAtTheEndOfPath(Expression<Func<PathBuildingTestClass, object>> path,
            string[] expectedParseResult)
        {
            SupportedPathsTestImpl(path, expectedParseResult);
        }

        public static IEnumerable<object[]> AddDependency_SupportedPaths_TestData
        {
            get
            {
                //Simple reference type property
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.StringProperty),
                    new[] {"root", "StringProperty"}
                };

                //Simple value type property
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.IntProperty),
                    new[] {"root", "IntProperty"}
                };

                //Simple reference type property chain
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.InnerProperty.StringProperty),
                    new[] {"root", "InnerProperty", "StringProperty"}
                };

                //Simple property chain with the value type object in the end of chain
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.InnerProperty.IntProperty),
                    new[] {"root", "InnerProperty", "IntProperty"}
                };

                //Simple reference type field
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.StringField),
                    new[] {"root", "StringField"}
                };

                //Simple value type field
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.IntField),
                    new[] {"root", "IntField"}
                };

                //Simple reference type field chain
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.InnerField.StringField),
                    new[] {"root", "InnerField", "StringField"}
                };

                //Simple field chain with the value type field in the end of chain
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.InnerField.IntField),
                    new[] {"root", "InnerField", "IntField"}
                };

                //Simple property chain with a field in the middle and reference type property in the end
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.InnerField.StringProperty),
                    new[] {"root", "InnerField", "StringProperty"}
                };

                //Simple property chain with a field in the middle and value type property in the end
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.InnerField.IntProperty),
                    new[] {"root", "InnerField", "IntProperty"}
                };

                //Simple reference type readonly field
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.StringFieldReadOnly),
                    new[] {"root", "StringFieldReadOnly"}
                };

                //Simple value type readonly field
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.IntFieldReadOnly),
                    new[] {"root", "IntFieldReadOnly"}
                };

                //Simple reference type readonly field -> field chain
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.InnerFieldReadOnly.StringField),
                    new[] {"root", "InnerFieldReadOnly", "StringField"}
                };

                //Simple reference type readonly field -> property chain
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.InnerFieldReadOnly.StringProperty),
                    new[] {"root", "InnerFieldReadOnly", "StringProperty"}
                };


                //Simple reference type field -> readonly field chain
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.InnerField.StringFieldReadOnly),
                    new[] {"root", "InnerField", "StringFieldReadOnly"}
                };

                //Simple property -> readonly field chain
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.InnerProperty.StringFieldReadOnly),
                    new[] {"root", "InnerProperty", "StringFieldReadOnly"}
                };

                //Simple reference type readonly field -> readonly field chain
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.InnerFieldReadOnly.StringFieldReadOnly),
                    new[] {"root", "InnerFieldReadOnly", "StringFieldReadOnly"}
                };

                //Simple readonly field -> field chain with value type field in the end of chain
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.InnerFieldReadOnly.IntField),
                    new[] {"root", "InnerFieldReadOnly", "IntField"}
                };

                //Simple readonly field -> readonly field chain with value type field in the end of chain
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.InnerFieldReadOnly.IntFieldReadOnly),
                    new[] {"root", "InnerFieldReadOnly", "IntFieldReadOnly"}
                };

                //Simple field -> readonly field chain with value type field in the end of chain
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.InnerField.IntFieldReadOnly),
                    new[] {"root", "InnerField", "IntFieldReadOnly"}
                };

                //Simple property chain with a readonly field in the middle and reference type property in the end
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.InnerFieldReadOnly.StringProperty),
                    new[] {"root", "InnerFieldReadOnly", "StringProperty"}
                };

                //Simple property chain with a readonly field in the middle and value type property in the end
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => o.InnerFieldReadOnly.IntProperty),
                    new[] {"root", "InnerFieldReadOnly", "IntProperty"}
                };

                //Property chain with collection item of reference type
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => DependenciesTracker.CollectionExtensions.EachElement(o.Strings)),
                    new[] {"root", "Strings", "CollectionItem"}
                };

                //Property chain with collection item of value type in the end of chain
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => DependenciesTracker.CollectionExtensions.EachElement(o.Ints)),
                    new[] {"root", "Ints", "CollectionItem"}
                };

                //Property chain with collection item in the middle of chain
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(o => DependenciesTracker.CollectionExtensions.EachElement(o.Strings).Length),
                    new[] {"root", "Strings", "CollectionItem", "Length"}
                };

                //Property chain with collection item of collection item in the middle of chain
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(
                        o => DependenciesTracker.CollectionExtensions.EachElement(DependenciesTracker.CollectionExtensions.EachElement(o.StringLists)).Length),
                    new[] {"root", "StringLists", "CollectionItem","CollectionItem", "Length"}
                };

                //Property chain with collection item of collection item in the end of chain
                yield return new object[]
                {
                    (Expression<Func<PathBuildingTestClass, object>>)(
                        o => DependenciesTracker.CollectionExtensions.EachElement(DependenciesTracker.CollectionExtensions.EachElement(o.IntLists))),
                    new[] {"root", "IntLists", "CollectionItem", "CollectionItem"}
                };
            }
        }

        [Theory]
        [PropertyData("AddDependency_SupportedPaths_TestData")]
        public void AddDependency_SupportedPaths(Expression<Func<PathBuildingTestClass, object>> path,
            string[] expectedParseResult)
        {
            SupportedPathsTestImpl(path, expectedParseResult);
        }

        //Reproduces the https://github.com/ademchenko/DependenciesTracker/issues/3
        [Fact]
        public void AddDependency_PropertyChainWithCollectionItemAtTheBegginning_Supported()
        {
            var map = new DependenciesMap<PathBuildingCollectionTestClass<string>>();
            map.AddMap(o => o.IntProperty, o => -1, o => DependenciesTracker.CollectionExtensions.EachElement(o));

            Assert.Equal(new[] { "root", "CollectionItem" }, map.MapItems.Single().PathStrings);
        }

        //Reproduces the https://github.com/ademchenko/DependenciesTracker/issues/3
        [Fact]
        public void AddDependency_PropertyChainWithCollectionItemAtTheBegginningAndPropertyInTheEnd_Supported()
        {
            var map = new DependenciesMap<PathBuildingCollectionTestClass<string>>();
            map.AddMap(o => o.IntProperty, o => -1, o => DependenciesTracker.CollectionExtensions.EachElement(o).Length);

            Assert.Equal(new[] { "root", "CollectionItem", "Length" }, map.MapItems.Single().PathStrings);
        }

        //Reproduces the https://github.com/ademchenko/DependenciesTracker/issues/3
        [Fact]
        public void AddDependency_PropertyChainWithCollectionItemOfCollectionItemAtTheBegginning_Supported()
        {
            var map = new DependenciesMap<PathBuildingCollectionTestClass<List<string>>>();
            map.AddMap(o => o.IntProperty, o => -1, o => DependenciesTracker.CollectionExtensions.EachElement(DependenciesTracker.CollectionExtensions.EachElement(o)));

            Assert.Equal(new[] { "root", "CollectionItem", "CollectionItem" }, map.MapItems.Single().PathStrings);
        }

        [Fact]
        public void AddDependency_PropertyChainWithCollectionItemOfCollectionItemAtTheBegginningAndPropertyInTheEnd_Supported()
        {
            var map = new DependenciesMap<PathBuildingCollectionTestClass<List<string>>>();
            map.AddMap(o => o.IntProperty, o => -1, o => DependenciesTracker.CollectionExtensions.EachElement(DependenciesTracker.CollectionExtensions.EachElement(o)).Length);

            Assert.Equal(new[] { "root", "CollectionItem", "CollectionItem", "Length" }, map.MapItems.Single().PathStrings);
        }

        [Fact]
        public void AddDependency_PrivatePropertiesInDependencyPath_Supported()
        {
            var random = new Random();
            var expectedPrice = random.Next(1, 100);
            var expectedQuantity = random.Next(100, 200);
            var expectedCost = expectedPrice * expectedQuantity;

            var mapItems = PathBuildingTestClassWithPrivatePropertiesAndFields.PropertiesDependenciesMap.MapItems;
            Assert.Equal(2, mapItems.Count);

            var pricePropertyPathItemWithRoot = mapItems.Single(mi => mi.PathStrings.Skip(1).Single() == "_priceProperty");
            var quantityPropertyPathItemWithRoot = mapItems.Single(mi => mi.PathStrings.Skip(1).Single() == "_quantityProperty");

            var pricePropertyPathItem = pricePropertyPathItemWithRoot.Ancestor;
            var quantityPropertyPathItem = quantityPropertyPathItemWithRoot.Ancestor;

            Assert.Equal("root", pricePropertyPathItemWithRoot.PathStrings.First());
            Assert.Equal("root", quantityPropertyPathItemWithRoot.PathStrings.First());

            //Test compiled getters

            var obj = PathBuildingTestClassWithPrivatePropertiesAndFields.CreateWithPrivateProperties(expectedPrice, expectedQuantity);

            var actualPrice = (int)((PropertyPathItem<PathBuildingTestClassWithPrivatePropertiesAndFields>)pricePropertyPathItem).PropertyOrFieldGetter(obj);
            var actualQuantity = (int)((PropertyPathItem<PathBuildingTestClassWithPrivatePropertiesAndFields>)quantityPropertyPathItem).PropertyOrFieldGetter(obj);

            Assert.Equal(expectedPrice, actualPrice);
            Assert.Equal(expectedQuantity, actualQuantity);

            //Test compiled setter
            Assert.Equal(0, obj.GetCostProperty());

            pricePropertyPathItem.UpdateDependentPropertyOrFieldAction(obj);

            Assert.Equal(expectedCost, obj.GetCostProperty());

            obj = PathBuildingTestClassWithPrivatePropertiesAndFields.CreateWithPrivateProperties(expectedPrice, expectedQuantity);

            Assert.Equal(0, obj.GetCostProperty());

            quantityPropertyPathItem.UpdateDependentPropertyOrFieldAction(obj);

            Assert.Equal(expectedCost, obj.GetCostProperty());
        }

        [Fact]
        public void AddDependency_PrivateFieldsInDependencyPath_Supported()
        {
            var random = new Random();
            var expectedPrice = random.Next(1, 100);
            var expectedQuantity = random.Next(100, 200);
            var expectedCost = expectedPrice * expectedQuantity;

            var mapItems = PathBuildingTestClassWithPrivatePropertiesAndFields.FieldsDependenciesMap.MapItems;
            Assert.Equal(2, mapItems.Count);

            var priceFieldPathItemWithRoot = mapItems.Single(mi => mi.PathStrings.Skip(1).Single() == "_priceField");
            var quantityFieldPathItemWithRoot = mapItems.Single(mi => mi.PathStrings.Skip(1).Single() == "_quantityField");

            var priceFieldPathItem = priceFieldPathItemWithRoot.Ancestor;
            var quantityFieldPathItem = quantityFieldPathItemWithRoot.Ancestor;

            Assert.Equal("root", priceFieldPathItemWithRoot.PathStrings.First());
            Assert.Equal("root", quantityFieldPathItemWithRoot.PathStrings.First());

            //Test compiled getters

            var obj = PathBuildingTestClassWithPrivatePropertiesAndFields.CreateWithPrivateFields(expectedPrice, expectedQuantity);

            var actualPrice = (int)((PropertyPathItem<PathBuildingTestClassWithPrivatePropertiesAndFields>)priceFieldPathItem).PropertyOrFieldGetter(obj);
            var actualQuantity = (int)((PropertyPathItem<PathBuildingTestClassWithPrivatePropertiesAndFields>)quantityFieldPathItem).PropertyOrFieldGetter(obj);

            Assert.Equal(expectedPrice, actualPrice);
            Assert.Equal(expectedQuantity, actualQuantity);

            //Test compiled setter
            Assert.Equal(0, obj.GetCostField());

            priceFieldPathItem.UpdateDependentPropertyOrFieldAction(obj);

            Assert.Equal(expectedCost, obj.GetCostField());

            obj = PathBuildingTestClassWithPrivatePropertiesAndFields.CreateWithPrivateFields(expectedPrice, expectedQuantity);

            Assert.Equal(0, obj.GetCostField());

            quantityFieldPathItem.UpdateDependentPropertyOrFieldAction(obj);

            Assert.Equal(expectedCost, obj.GetCostField());
        }

        private static void SupportedPathsTestImpl(Expression<Func<PathBuildingTestClass, object>> path, string[] expectedParseResult)
        {
            var map = new DependenciesMap<PathBuildingTestClass>();

            map.AddMap(o => o.DependentProperty, o => -1, path);

            Assert.Equal(expectedParseResult, map.MapItems.Single().PathStrings);
        }
    }
}