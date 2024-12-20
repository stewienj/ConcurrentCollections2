﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Swordfish.NET.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Swordfish.NET.UnitTestV3
{
    [TestClass]
    public class ConcurrentObservableDictionaryTests
    {
        [TestMethod]
        public void AddRangeValueTypeTest()
        {
            // There was an issue with ConcurrentObservableDictionary.AddMany throwing an
            // exception when passed an IEnumerable.

            IEnumerable<KeyValuePair<string,string>> GetIEnumerable()
            {
                for(int i=0; i<10; ++i)
                {
                    yield return new KeyValuePair<string, string>(i.ToString(), i.ToString());
                }
            }

            var itemsToAdd = GetIEnumerable();
            var dictionary1 = new ConcurrentObservableDictionary<string,string>();
            dictionary1.AddRange(itemsToAdd);

            Assert.IsTrue(dictionary1.Count == itemsToAdd.Count(), "Right number of items");

            var sourceDictionary = itemsToAdd.ToDictionary(a => a.Key, b => b.Value);
            var dictionary2 = new ConcurrentObservableDictionary<string, string>();
            dictionary2.AddRange(sourceDictionary);

            Assert.IsTrue(dictionary2.Count == sourceDictionary.Count, "Right number of items");
        }

        [TestMethod]
        public void AddRangeReferenceTypeTest()
        {
            // There was an issue with ConcurrentObservableDictionary.AddMany throwing an
            // exception when passed an IEnumerable.

            IEnumerable<KeyValuePair<KeyStringTest, string>> GetIEnumerable()
            {
                for (int i = 0; i < 10; ++i)
                {
                    yield return new KeyValuePair<KeyStringTest, string>(new KeyStringTest(i.ToString()), i.ToString());
                }
            }

            var itemsToAdd = GetIEnumerable().ToArray();
            var dictionary1 = new ConcurrentObservableDictionary<KeyStringTest, string>();
            dictionary1.AddRange(itemsToAdd);

            Assert.IsTrue(dictionary1.Count == itemsToAdd.Count(), "Right number of items");

            var sourceDictionary = itemsToAdd.ToDictionary(a => a.Key, b => b.Value);
            var dictionary2 = new ConcurrentObservableDictionary<KeyStringTest, string>();
            dictionary2.AddRange(sourceDictionary);

            Assert.IsTrue(dictionary2.Count == sourceDictionary.Count, "Right number of items");
        }

        [TestMethod]
        public void AddValueTypeKeysTest()
        {
            int testCollectionCount = 10;
            int itemsPerCollection = 200_000;
            var sourceCollections = new List<List<int>>();

            // Use a fixed seed for consistency in results
            Random random = new Random(1);

            // Create 10 test sets
            for (int collection = 0; collection < testCollectionCount; ++collection)
            {
                List<int> sourceCollection = new List<int>();
                for (int item = 0; item < itemsPerCollection; ++item)
                {
                    // Ensure we have some duplicates by picking a random number
                    // less than half the number of items.
                    sourceCollection.Add(random.Next(itemsPerCollection / 2));
                }
                sourceCollections.Add(sourceCollection);
            }

            var testCollection = new ConcurrentObservableDictionary<int, int>();

            // Create test subject
            // Populate test subject
            sourceCollections.AsParallel().ForAll(collection =>
            {
                foreach (var item in collection)
                {
                    testCollection[item] = item;
                }
            });

            bool keyMatchesValue = testCollection
                .All(kv => kv.Key == kv.Value);

            Assert.IsTrue(keyMatchesValue, "Keys match values");

            var sorted =
                sourceCollections
                .SelectMany(x => x)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var sortedFromTest =
                testCollection
                .OrderBy(x => x.Key)
                .ToList();

            Assert.IsTrue(sorted.Count == sortedFromTest.Count, "Right number of items");

            var allItemsPresent =
                sorted
                .Zip(sortedFromTest, (a, b) => a == b.Key)
                .All(a => a);

            Assert.IsTrue(allItemsPresent, "All items present");
        }

        [TestMethod]
        public void TestTryRemoveValueType()
        {
            var testCollection = new ConcurrentObservableDictionary<string, string>();
            for (int i = 0; i < 10; i++)
            {
                testCollection.Add($"Key{i}", $"Value{i}");
            }
            Assert.AreEqual(10, testCollection.Count);
            Assert.IsFalse(testCollection.TryRemove("Key10", out var value));
            for (int i = 9; i >= 0; i--)
            {
                string key = $"Key{i}";
                string expectedValue = $"Value{i}";
                Assert.IsTrue(testCollection.TryRemove(key, out var item));
                Assert.AreEqual(expectedValue, item);
                Assert.AreEqual(i, testCollection.Count);
            }
        }

        [TestMethod]
        public void TestTryRemoveReferenceType()
        {
            var keys = new KeyStringTest[10];
            var testCollection = new ConcurrentObservableDictionary<KeyStringTest, string>();
            for (int i = 0; i < 10; i++)
            {
                keys[i] = new KeyStringTest($"Key{i}");
                testCollection.Add(keys[i], $"Value{i}");
            }
            Assert.AreEqual(10, testCollection.Count);
            Assert.IsFalse(testCollection.TryRemove(new KeyStringTest("Key10"), out var value));
            for (int i = 9; i >= 0; i--)
            {
                KeyStringTest key = keys[i];
                string expectedValue = $"Value{i}";
                Assert.IsTrue(testCollection.TryRemove(key, out var item));
                Assert.AreEqual(expectedValue, item);
                Assert.AreEqual(i, testCollection.Count);
            }
        }


        [TestMethod]
        public void TestManyOperationsValueTypeKeys()
        {
            // Create some random, but unique items
            // Use a fixed seed for consistency in results
            Random random = new Random(1);
            HashSet<int> baseItemsSet = new HashSet<int>();
            while (baseItemsSet.Count < 1_100_000)
            {
                baseItemsSet.Add(random.Next());
            }

            // Create 2 collections, 1 to test, and 1 to compare against
            var testCollection = new ConcurrentObservableDictionary<string, string>();
            var list = new List<KeyValuePair<string, string>>();

            // Create 1,000,000 items to add and insert
            var itemsToAdd =
                baseItemsSet
                .Take(1_000_000)
                .Select(x => Swordfish.NET.Collections.KeyValuePair.Create($"Key {x}", $"Value {x}"))
                .ToList();

            // Create 100,000 items to insert
            var itemsToInsert =
                baseItemsSet
                .Skip(1_000_000)
                .Take(100_000)
                .Select(x => Swordfish.NET.Collections.KeyValuePair.Create($"Insert Key {x}", $"Insert Value {x}"))
                .ToList();

            // Create items to remove
            var itemsToRemove =
                itemsToInsert
                .Take(1000)
                .ToList();

            foreach (var item in itemsToAdd)
            {
                testCollection.Add(item.Key, item.Value);
                list.Add(item);
            }

            // Check items are equal count
            Assert.IsTrue(list.Count == testCollection.Count, "Added Items correct count");

            // Check items are equal order
            var allEqualAfterAdd =
                list
                .Zip(testCollection, (a, b) => (a.Key == b.Key) && (a.Value == b.Value))
                .All(a => a);

            Assert.IsTrue(allEqualAfterAdd, "Added items correct order");

            // Test inserting items

            int insertIndex = itemsToInsert.Count + 100;
            foreach (var item in itemsToInsert)
            {
                // We have the function but it's there for other reasons
                testCollection.Insert(insertIndex, item);

                list.Insert(insertIndex, item);

                insertIndex--;
            }

            // Check items are equal count
            Assert.IsTrue(list.Count == testCollection.Count, "Items correct count after inserting");

            // Check items are equal order
            var allEqualAfterInsert =
                list
                .Zip(testCollection, (a, b) => (a.Key == b.Key) && (a.Value == b.Value))
                .All(a => a);

            Assert.IsTrue(allEqualAfterAdd, "Items correct order after insert");

            // Test removing items

            foreach (var item in itemsToRemove)
            {
                testCollection.Remove(item.Key);
                list.Remove(item);
            }

            // Check items are equal count
            Assert.IsTrue(list.Count == testCollection.Count, "Items correct count after removing");

            // Check items are equal order
            var allEqualAfterRemove =
                list
                .Zip(testCollection, (a, b) => (a.Key == b.Key) && (a.Value == b.Value))
                .All(a => a);

            Assert.IsTrue(allEqualAfterRemove, "Items correct order after removing");

            // Test contains

            var containsAll = list
                .All(kv => testCollection.Contains(kv));

            Assert.IsTrue(containsAll, "Contains all the items is true");

            var containsNone = itemsToRemove
                .Any(kv => testCollection.ContainsKey(kv.Key));

            Assert.IsFalse(containsNone, "Contains any of the removed items is false");

            // Test removing at

            int removeAtIndex = list.Count - 30;
            while (removeAtIndex >= 0 && list.Count > 0)
            {
                list.RemoveAt(removeAtIndex);
                testCollection.RemoveAt(removeAtIndex);
                removeAtIndex -= 30;
            }

            // Check items are equal count
            Assert.IsTrue(list.Count == testCollection.Count, "Items correct count after removing at index");

            // Check items are equal order
            var allEqualAfterRemoveAt =
                list
                .Zip(testCollection, (a, b) => (a.Key == b.Key) && (a.Value == b.Value))
                .All(a => a);

            Assert.IsTrue(allEqualAfterRemoveAt, "Items correct order after removing at index");
        }

        [TestMethod]
        public void TestManyOperationsReferenceTypeKeys()
        {
            // Create some random, but unique items
            // Use a fixed seed for consistency in results
            Random random = new Random(1);
            HashSet<int> baseItemsSet = new HashSet<int>();
            while (baseItemsSet.Count < 1_100_000)
            {
                baseItemsSet.Add(random.Next());
            }

            // Create 2 collections, 1 to test, and 1 to compare against
            var testCollection = new ConcurrentObservableDictionary<KeyStringTest, string>();
            var list = new List<KeyValuePair<KeyStringTest, string>>();

            // Create 1,000,000 items to add and insert
            var itemsToAdd =
                baseItemsSet
                .Take(1_000_000)
                .Select(x => Swordfish.NET.Collections.KeyValuePair.Create(new KeyStringTest($"Key {x}"), $"Value {x}"))
                .ToList();

            // Create 100,000 items to insert
            var itemsToInsert =
                baseItemsSet
                .Skip(1_000_000)
                .Take(100_000)
                .Select(x => Swordfish.NET.Collections.KeyValuePair.Create(new KeyStringTest($"Insert Key {x}"), $"Insert Value {x}"))
                .ToList();

            // Create items to remove
            var itemsToRemove =
                itemsToInsert
                .Take(1000)
                .ToList();

            foreach (var item in itemsToAdd)
            {
                testCollection.Add(item.Key, item.Value);
                list.Add(item);
            }

            // Check items are equal count
            Assert.IsTrue(list.Count == testCollection.Count, "Added Items correct count");

            // Check items are equal order
            var allEqualAfterAdd =
                list
                .Zip(testCollection, (a, b) => (a.Key == b.Key) && (a.Value == b.Value))
                .All(a => a);

            Assert.IsTrue(allEqualAfterAdd, "Added items correct order");

            // Test inserting items

            int insertIndex = itemsToInsert.Count + 100;
            foreach (var item in itemsToInsert)
            {
                // We have the function but it's there for other reasons
                testCollection.Insert(insertIndex, item);

                list.Insert(insertIndex, item);

                insertIndex--;
            }

            // Check items are equal count
            Assert.IsTrue(list.Count == testCollection.Count, "Items correct count after inserting");

            // Check items are equal order
            var allEqualAfterInsert =
                list
                .Zip(testCollection, (a, b) => (a.Key == b.Key) && (a.Value == b.Value))
                .All(a => a);

            Assert.IsTrue(allEqualAfterAdd, "Items correct order after insert");

            // Test removing items

            foreach (var item in itemsToRemove)
            {
                testCollection.Remove(item.Key);
                list.Remove(item);
            }

            // Check items are equal count
            Assert.IsTrue(list.Count == testCollection.Count, "Items correct count after removing");

            // Check items are equal order
            var allEqualAfterRemove =
                list
                .Zip(testCollection, (a, b) => (a.Key == b.Key) && (a.Value == b.Value))
                .All(a => a);

            Assert.IsTrue(allEqualAfterRemove, "Items correct order after removing");

            // Test contains

            var containsAll = list
                .All(kv => testCollection.Contains(kv));

            Assert.IsTrue(containsAll, "Contains all the items is true");

            var containsNone = itemsToRemove
                .Any(kv => testCollection.ContainsKey(kv.Key));

            Assert.IsFalse(containsNone, "Contains any of the removed items is false");

            // Test removing at

            int removeAtIndex = list.Count - 30;
            while (removeAtIndex >= 0 && list.Count > 0)
            {
                list.RemoveAt(removeAtIndex);
                testCollection.RemoveAt(removeAtIndex);
                removeAtIndex -= 30;
            }

            // Check items are equal count
            Assert.IsTrue(list.Count == testCollection.Count, "Items correct count after removing at index");

            // Check items are equal order
            var allEqualAfterRemoveAt =
                list
                .Zip(testCollection, (a, b) => (a.Key == b.Key) && (a.Value == b.Value))
                .All(a => a);

            Assert.IsTrue(allEqualAfterRemoveAt, "Items correct order after removing at index");
        }

        /// <summary>
        /// Tests IndexOf, uses a value type for the key
        /// </summary>
        [TestMethod]
        public void TestIndexOfValueTypeInViews()
        {
            var initial = Enumerable.Range(0, 100).Select(x => new KeyValuePair<int, string>(x, x.ToString())).ToList();
            var other = Enumerable.Range(100, 100).Select(x => new KeyValuePair<int, string>(x, x.ToString())).ToList();
            var testCollection = new ConcurrentObservableDictionary<int, string>();
            testCollection.AddRange(initial);
            var collectionView = testCollection.CollectionView;
            var keysView = testCollection.Keys;
            var valuesView = testCollection.Values;

            // Test the IList implementation because it had a bug

            IList collectionList = (IList)testCollection.CollectionView;
            IList keysList = (IList)testCollection.Keys;
            IList valuesList = (IList)testCollection.Values;

            foreach (var item in initial)
            {
                Assert.IsTrue(testCollection.Contains(item));

                Assert.IsTrue(collectionView.Contains(item));
                Assert.IsTrue(keysView.Contains(item.Key));
                Assert.IsTrue(valuesView.Contains(item.Value));

                Assert.IsTrue(collectionList.Contains(item));
                Assert.IsTrue(keysList.Contains(item.Key));
                Assert.IsTrue(valuesList.Contains(item.Value));
            }

            foreach (var item in other)
            {
                Assert.IsFalse(testCollection.Contains(item));

                Assert.IsFalse(collectionView.Contains(item));
                Assert.IsFalse(keysView.Contains(item.Key));
                Assert.IsFalse(valuesView.Contains(item.Value));

                Assert.IsFalse(collectionList.Contains(item));
                Assert.IsFalse(keysList.Contains(item.Key));
                Assert.IsFalse(valuesList.Contains(item.Value));
            }

            for (int i=0; i<initial.Count; ++i)
            {
                Assert.AreEqual(initial[i], collectionView[i]);
                Assert.AreEqual(initial[i].Key, keysView[i]);
                Assert.AreEqual(initial[i].Value, valuesView[i]);

                Assert.AreEqual(initial[i], collectionList[i]);
                Assert.AreEqual(initial[i].Key, keysList[i]);
                Assert.AreEqual(initial[i].Value, valuesList[i]);

                Assert.AreEqual(i, collectionView.IndexOf(initial[i]));
                Assert.AreEqual(i, keysView.IndexOf(initial[i].Key));
                Assert.AreEqual(i, valuesView.IndexOf(initial[i].Value));

                Assert.AreEqual(i, collectionList.IndexOf(initial[i]));
                Assert.AreEqual(i, keysList.IndexOf(initial[i].Key));
                Assert.AreEqual(i, valuesList.IndexOf(initial[i].Value));
            }
        }

        /// <summary>
        /// Tests IndexOf, uses a reference type for the key
        /// </summary>
        [TestMethod]
        public void TestIndexOfReferenceTypeInViews()
        {
            var initial = Enumerable.Range(0, 100).Select(x => new KeyValuePair<KeyIntTest, string>(new KeyIntTest(x), x.ToString())).ToList();
            var other = Enumerable.Range(100, 100).Select(x => new KeyValuePair<KeyIntTest, string>(new KeyIntTest(x), x.ToString())).ToList();
            var testCollection = new ConcurrentObservableDictionary<KeyIntTest, string>();
            testCollection.AddRange(initial);
            var collectionView = testCollection.CollectionView;
            var keysView = testCollection.Keys;
            var valuesView = testCollection.Values;

            // Test the IList implementation because it had a bug

            IList collectionList = (IList)testCollection.CollectionView;
            IList keysList = (IList)testCollection.Keys;
            IList valuesList = (IList)testCollection.Values;

            foreach (var item in initial)
            {
                Assert.IsTrue(testCollection.Contains(item));

                Assert.IsTrue(collectionView.Contains(item));
                Assert.IsTrue(keysView.Contains(item.Key));
                Assert.IsTrue(valuesView.Contains(item.Value));

                Assert.IsTrue(collectionList.Contains(item));
                Assert.IsTrue(keysList.Contains(item.Key));
                Assert.IsTrue(valuesList.Contains(item.Value));
            }

            foreach (var item in other)
            {
                Assert.IsFalse(testCollection.Contains(item));

                Assert.IsFalse(collectionView.Contains(item));
                Assert.IsFalse(keysView.Contains(item.Key));
                Assert.IsFalse(valuesView.Contains(item.Value));

                Assert.IsFalse(collectionList.Contains(item));
                Assert.IsFalse(keysList.Contains(item.Key));
                Assert.IsFalse(valuesList.Contains(item.Value));
            }

            for (int i = 0; i < initial.Count; ++i)
            {
                Assert.AreEqual(initial[i], collectionView[i]);
                Assert.AreEqual(initial[i].Key, keysView[i]);
                Assert.AreEqual(initial[i].Value, valuesView[i]);

                Assert.AreEqual(initial[i], collectionList[i]);
                Assert.AreEqual(initial[i].Key, keysList[i]);
                Assert.AreEqual(initial[i].Value, valuesList[i]);

                Assert.AreEqual(i, collectionView.IndexOf(initial[i]));
                Assert.AreEqual(i, keysView.IndexOf(initial[i].Key));
                Assert.AreEqual(i, valuesView.IndexOf(initial[i].Value));

                Assert.AreEqual(i, collectionList.IndexOf(initial[i]));
                Assert.AreEqual(i, keysList.IndexOf(initial[i].Key));
                Assert.AreEqual(i, valuesList.IndexOf(initial[i].Value));
            }
        }

        [TestMethod]
        public void AddRemoveRangeValueTypeTest()
        {
            IEnumerable<KeyValuePair<string, string>> GetIEnumerable()
            {
                for (int i = 0; i < 10; ++i)
                {
                    yield return new KeyValuePair<string, string>(i.ToString(), i.ToString());
                }
            }

            // Don't need to convert to array because value type
            var itemsToAdd = GetIEnumerable();
            var dictionary1 = new ConcurrentObservableDictionary<string, string>();
            dictionary1.AddRange(itemsToAdd);

            Assert.IsTrue(dictionary1.Count == itemsToAdd.Count(), "Count doesn't match number of items added");

            foreach (var item in itemsToAdd)
            {
                Assert.IsTrue(dictionary1.Remove(item.Key), "Problem removing item");
            }

            Assert.IsTrue(dictionary1.Count == 0, "Not all items removed");
        }

        [TestMethod]
        public void AddRemoveRangeReferenceTypeTest()
        {
            IEnumerable<KeyValuePair<KeyStringTest, string>> GetIEnumerable()
            {
                for (int i = 0; i < 10; ++i)
                {
                    yield return new KeyValuePair<KeyStringTest, string>(new KeyStringTest(i.ToString()), i.ToString());
                }
            }

            // Need to convert to array so we have the same objects being used
            var itemsToAdd = GetIEnumerable().ToArray();
            var dictionary1 = new ConcurrentObservableDictionary<KeyStringTest, string>();
            dictionary1.AddRange(itemsToAdd);

            Assert.IsTrue(dictionary1.Count == itemsToAdd.Count(), "Count doesn't match number of items added");

            foreach (var item in itemsToAdd)
            {
                Assert.IsTrue(dictionary1.Remove(item.Key), "Problem removing item");
            }

            Assert.IsTrue(dictionary1.Count == 0, "Not all items removed");
        }

        [TestMethod]
        public void AddRemoveRangeReferenceTypeWithOverrideTest()
        {
            IEnumerable<KeyValuePair<KeyStringWithOverride, string>> GetIEnumerable()
            {
                for (int i = 0; i < 10; ++i)
                {
                    yield return new KeyValuePair<KeyStringWithOverride, string>(new KeyStringWithOverride(i.ToString()), i.ToString());
                }
            }

            // Don't convert to array, testing the use of reference types that override Equals and GetHashCode
            var itemsToAdd = GetIEnumerable();
            var dictionary1 = new ConcurrentObservableDictionary<KeyStringWithOverride, string>();
            dictionary1.AddRange(itemsToAdd);

            Assert.IsTrue(dictionary1.Count == itemsToAdd.Count(), "Count doesn't match number of items added");

            foreach (var item in itemsToAdd)
            {
                Assert.IsTrue(dictionary1.Remove(item.Key), "Problem removing item");
            }

            Assert.IsTrue(dictionary1.Count == 0, "Not all items removed");
        }

        /// <summary>
        /// Uses Add() instead of AddRange()
        /// </summary>
        [TestMethod]
        public void AddRemoveReferenceTypeTest()
        {
            // There was an issue with ConcurrentObservableDictionary.AddMany throwing an
            // exception when passed an IEnumerable.

            IEnumerable<KeyValuePair<KeyStringTest, string>> GetIEnumerable()
            {
                for (int i = 0; i < 10; ++i)
                {
                    yield return new KeyValuePair<KeyStringTest, string>(new KeyStringTest(i.ToString()), i.ToString());
                }
            }

            var itemsToAdd = GetIEnumerable().ToArray();
            var dictionary1 = new ConcurrentObservableDictionary<KeyStringTest, string>();
            // Use Add() instead of AddRange()
            foreach(var item in itemsToAdd)
            {
                dictionary1.Add(item);
            }

            Assert.IsTrue(dictionary1.Count == itemsToAdd.Count(), "Count doesn't match number of items added");

            foreach (var item in itemsToAdd)
            {
                Assert.IsTrue(dictionary1.Remove(item.Key), "Problem removing item");
            }

            Assert.IsTrue(dictionary1.Count == 0, "Not all items removed");
        }

        /// <summary>
        /// Uses Add() instead of AddRange()
        /// </summary>
        [TestMethod]
        public void AddRemoveReferenceTypeWithOverrideTest()
        {
            // There was an issue with ConcurrentObservableDictionary.AddMany throwing an
            // exception when passed an IEnumerable.

            IEnumerable<KeyValuePair<KeyStringWithOverride, string>> GetIEnumerable()
            {
                for (int i = 0; i < 10; ++i)
                {
                    yield return new KeyValuePair<KeyStringWithOverride, string>(new KeyStringWithOverride(i.ToString()), i.ToString());
                }
            }

            // Don't convert to array, testing the use of reference types that override Equals and GetHashCode
            var itemsToAdd = GetIEnumerable();
            var dictionary1 = new ConcurrentObservableDictionary<KeyStringWithOverride, string>();
            // Use Add() instead of AddRange()
            foreach (var item in itemsToAdd)
            {
                dictionary1.Add(item);
            }

            Assert.IsTrue(dictionary1.Count == itemsToAdd.Count(), "Count doesn't match number of items added");

            foreach (var item in itemsToAdd)
            {
                Assert.IsTrue(dictionary1.Remove(item.Key), "Problem removing item");
            }

            Assert.IsTrue(dictionary1.Count == 0, "Not all items removed");
        }
    }
}
