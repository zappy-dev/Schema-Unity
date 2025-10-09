using Schema.Core.DataStructures;

namespace Schema.Core.Tests.DataStructures;

[TestFixture]
public class TestLRUCache
{
    [Test]
    public void Test_Constructor_ValidCapacity()
    {
        var cache = new LRUCache<int, string>(5);
        Assert.That(cache, Is.Not.Null);
    }

    [Test]
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-100)]
    public void Test_Constructor_InvalidCapacity_ThrowsArgumentException(int capacity)
    {
        Assert.Throws<ArgumentException>(() => new LRUCache<int, string>(capacity));
    }

    [Test]
    public void Test_TryGet_EmptyCache_ReturnsFalse()
    {
        var cache = new LRUCache<int, string>(5);
        var result = cache.TryGet(1, out var value);
        
        Assert.That(result, Is.False);
        Assert.That(value, Is.Null);
    }

    [Test]
    public void Test_Put_SingleItem_CanRetrieve()
    {
        var cache = new LRUCache<int, string>(5);
        cache.Put(1, "one");
        
        var result = cache.TryGet(1, out var value);
        
        Assert.That(result, Is.True);
        Assert.That(value, Is.EqualTo("one"));
    }

    [Test]
    public void Test_Put_MultipleItems_CanRetrieveAll()
    {
        var cache = new LRUCache<int, string>(5);
        cache.Put(1, "one");
        cache.Put(2, "two");
        cache.Put(3, "three");
        
        Assert.That(cache.TryGet(1, out var value1), Is.True);
        Assert.That(value1, Is.EqualTo("one"));
        
        Assert.That(cache.TryGet(2, out var value2), Is.True);
        Assert.That(value2, Is.EqualTo("two"));
        
        Assert.That(cache.TryGet(3, out var value3), Is.True);
        Assert.That(value3, Is.EqualTo("three"));
    }

    [Test]
    public void Test_Put_ExceedsCapacity_EvictsLeastRecentlyUsed()
    {
        var cache = new LRUCache<int, string>(3);
        cache.Put(1, "one");
        cache.Put(2, "two");
        cache.Put(3, "three");
        
        // Adding a fourth item should evict the first one (least recently used)
        cache.Put(4, "four");
        
        Assert.That(cache.TryGet(1, out _), Is.False, "Item 1 should have been evicted");
        Assert.That(cache.TryGet(2, out _), Is.True, "Item 2 should still be in cache");
        Assert.That(cache.TryGet(3, out _), Is.True, "Item 3 should still be in cache");
        Assert.That(cache.TryGet(4, out _), Is.True, "Item 4 should be in cache");
    }

    [Test]
    public void Test_Put_UpdateExistingKey_UpdatesValue()
    {
        var cache = new LRUCache<int, string>(5);
        cache.Put(1, "original");
        cache.Put(1, "updated");
        
        var result = cache.TryGet(1, out var value);
        
        Assert.That(result, Is.True);
        Assert.That(value, Is.EqualTo("updated"));
    }

    [Test]
    public void Test_Put_UpdateExistingKey_MovesToFront()
    {
        var cache = new LRUCache<int, string>(3);
        cache.Put(1, "one");
        cache.Put(2, "two");
        cache.Put(3, "three");
        
        // Update key 1, moving it to the front
        cache.Put(1, "one-updated");
        
        // Adding a fourth item should evict key 2 (now least recently used)
        cache.Put(4, "four");
        
        Assert.That(cache.TryGet(1, out var value1), Is.True, "Item 1 should still be in cache");
        Assert.That(value1, Is.EqualTo("one-updated"));
        Assert.That(cache.TryGet(2, out _), Is.False, "Item 2 should have been evicted");
        Assert.That(cache.TryGet(3, out _), Is.True, "Item 3 should still be in cache");
        Assert.That(cache.TryGet(4, out _), Is.True, "Item 4 should be in cache");
    }

    [Test]
    public void Test_TryGet_MovesItemToFront()
    {
        var cache = new LRUCache<int, string>(3);
        cache.Put(1, "one");
        cache.Put(2, "two");
        cache.Put(3, "three");
        
        // Access key 1, moving it to the front
        cache.TryGet(1, out _);
        
        // Adding a fourth item should evict key 2 (now least recently used)
        cache.Put(4, "four");
        
        Assert.That(cache.TryGet(1, out _), Is.True, "Item 1 should still be in cache");
        Assert.That(cache.TryGet(2, out _), Is.False, "Item 2 should have been evicted");
        Assert.That(cache.TryGet(3, out _), Is.True, "Item 3 should still be in cache");
        Assert.That(cache.TryGet(4, out _), Is.True, "Item 4 should be in cache");
    }

    [Test]
    public void Test_CapacityOne_WorksCorrectly()
    {
        var cache = new LRUCache<int, string>(1);
        cache.Put(1, "one");
        
        Assert.That(cache.TryGet(1, out var value1), Is.True);
        Assert.That(value1, Is.EqualTo("one"));
        
        // Adding a second item should evict the first
        cache.Put(2, "two");
        
        Assert.That(cache.TryGet(1, out _), Is.False);
        Assert.That(cache.TryGet(2, out var value2), Is.True);
        Assert.That(value2, Is.EqualTo("two"));
    }

    [Test]
    public void Test_WithStringKeys_WorksCorrectly()
    {
        var cache = new LRUCache<string, int>(3);
        cache.Put("a", 1);
        cache.Put("b", 2);
        cache.Put("c", 3);
        
        Assert.That(cache.TryGet("a", out var valueA), Is.True);
        Assert.That(valueA, Is.EqualTo(1));
        
        // Eviction test
        cache.Put("d", 4);
        
        Assert.That(cache.TryGet("b", out _), Is.False, "Item 'b' should have been evicted");
        Assert.That(cache.TryGet("a", out _), Is.True, "Item 'a' should still be in cache");
    }

    [Test]
    public void Test_WithComplexTypes_WorksCorrectly()
    {
        var cache = new LRUCache<string, List<int>>(2);
        var list1 = new List<int> { 1, 2, 3 };
        var list2 = new List<int> { 4, 5, 6 };
        
        cache.Put("first", list1);
        cache.Put("second", list2);
        
        Assert.That(cache.TryGet("first", out var retrievedList1), Is.True);
        Assert.That(retrievedList1, Is.EqualTo(list1));
        
        Assert.That(cache.TryGet("second", out var retrievedList2), Is.True);
        Assert.That(retrievedList2, Is.EqualTo(list2));
    }

    [Test]
    public void Test_LRUOrdering_ComplexScenario()
    {
        var cache = new LRUCache<int, string>(4);
        
        // Fill the cache
        cache.Put(1, "one");
        cache.Put(2, "two");
        cache.Put(3, "three");
        cache.Put(4, "four");
        
        // Access items in a specific order to change recency
        cache.TryGet(2, out _); // 2 is now most recent
        cache.TryGet(4, out _); // 4 is now most recent
        cache.TryGet(1, out _); // 1 is now most recent
        // Order now (from most to least recent): 1, 4, 2, 3
        
        // Add two new items, should evict 3 and then 2
        cache.Put(5, "five");
        Assert.That(cache.TryGet(3, out _), Is.False, "Item 3 should have been evicted");
        
        cache.Put(6, "six");
        Assert.That(cache.TryGet(2, out _), Is.False, "Item 2 should have been evicted");
        
        // Verify remaining items
        Assert.That(cache.TryGet(1, out _), Is.True, "Item 1 should still be in cache");
        Assert.That(cache.TryGet(4, out _), Is.True, "Item 4 should still be in cache");
        Assert.That(cache.TryGet(5, out _), Is.True, "Item 5 should be in cache");
        Assert.That(cache.TryGet(6, out _), Is.True, "Item 6 should be in cache");
    }

    [Test]
    public void Test_Put_NullValue_WorksCorrectly()
    {
        var cache = new LRUCache<int, string?>(5);
        cache.Put(1, null);
        
        var result = cache.TryGet(1, out var value);
        
        Assert.That(result, Is.True);
        Assert.That(value, Is.Null);
    }

    [Test]
    public void Test_TryGet_NonExistentKey_ReturnsDefaultValue()
    {
        var cache = new LRUCache<int, int>(5);
        cache.Put(1, 100);
        
        var result = cache.TryGet(999, out var value);
        
        Assert.That(result, Is.False);
        Assert.That(value, Is.EqualTo(0)); // default(int)
    }

    [Test]
    public void Test_MultipleUpdates_MaintainsCorrectState()
    {
        var cache = new LRUCache<int, string>(3);
        
        cache.Put(1, "v1");
        cache.Put(2, "v2");
        cache.Put(3, "v3");
        
        // Update values multiple times
        cache.Put(1, "v1-updated");
        cache.Put(2, "v2-updated");
        cache.Put(1, "v1-final");
        
        Assert.That(cache.TryGet(1, out var value1), Is.True);
        Assert.That(value1, Is.EqualTo("v1-final"));
        
        Assert.That(cache.TryGet(2, out var value2), Is.True);
        Assert.That(value2, Is.EqualTo("v2-updated"));
        
        // Item 3 should be the LRU now
        cache.Put(4, "v4");
        Assert.That(cache.TryGet(3, out _), Is.False, "Item 3 should have been evicted");
    }
}

