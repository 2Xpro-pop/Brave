using NSubstitute;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;

namespace Brave.Tests;

[TestFixture]
public sealed class ObservableExpressionTests
{
    private sealed class RecordingObserver : IObserver<object?>
    {
        public readonly List<object?> Values = new();
        public int CompletedCount;
        public Exception? Error;

        public void OnNext(object? value) => Values.Add(value);
        public void OnCompleted() => CompletedCount++;
        public void OnError(Exception error) => Error = error;
    }

    private static (IAbstractResources resources, IDictionary backing) Create()
    {
        var (resources, backing) = ResourcesMock.CreateResources();

        return (resources, backing);
    }

    [Test]
    public void Notifies_When_Dependency_Changes()
    {
        var (resources, _) = Create();

        resources["$a"] = 1;
        resources["$b"] = 2;

        using var observable = new ObservableExpression(resources, "$a + $b");
        var observer = new RecordingObserver();

        using var sub = observable.Subscribe(observer);

        resources["$a"] = 10;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observer.Error, Is.Null);
            Assert.That(observer.Values, Has.Count.EqualTo(2));
            Assert.That(observer.Values[0], Is.EqualTo(3));
            Assert.That(observer.Values[1], Is.EqualTo(12));
        }
    }

    [Test]
    public void DoesNotNotify_When_Unrelated_Key_Changes()
    {
        var (resources, _) = Create();

        resources["$a"] = 1;

        using var observable = new ObservableExpression(resources, "$a");
        var observer = new RecordingObserver();

        using var sub = observable.Subscribe(observer);

        resources["$b"] = 123;

        Assert.That(observer.Values, Is.EqualTo(new object?[] { 1 }));

        resources["$a"] = 2;

        Assert.That(observer.Values, Is.EqualTo(new object?[] { 1, 2 }));
    }

    [Test]
    public void Watches_Keys_Even_If_ControlFlow_May_Skip_Them()
    {
        var (resources, _) = Create();

        resources["$a"] = "A";
        resources["$b"] = "B";

        using var observable = new ObservableExpression(resources, "$a ?? $b");
        var observer = new RecordingObserver();

        using var sub = observable.Subscribe(observer);

        resources["$b"] = "B2";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observer.Values, Has.Count.EqualTo(1));
            Assert.That(observer.Values[0], Is.EqualTo("A"));
        }

        resources["$a"] = null;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observer.Values, Has.Count.EqualTo(2));
            Assert.That(observer.Values[1], Is.EqualTo("B2"));
        }
    }

    [Test]
    public void Unsubscribe_Stops_Notifications_For_Observer()
    {
        var (resources, _) = Create();

        resources["$a"] = 1;

        using var observable = new ObservableExpression(resources, "$a");
        var observer = new RecordingObserver();

        var sub = observable.Subscribe(observer);

        resources["$a"] = 2;
        Assert.That(observer.Values, Is.EqualTo(new object?[] { 1, 2 }));

        sub.Dispose();

        resources["$a"] = 3;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(observer.Values, Is.EqualTo(new object?[] { 1, 2 }));
            Assert.That(observer.CompletedCount, Is.EqualTo(0));
        }
    }

    [Test]
    public void Dispose_Completes_Observers_And_NoMore_OnNext()
    {
        var (resources, _) = Create();

        resources["$a"] = 1;

        var observable = new ObservableExpression(resources, "$a");
        var observer = new RecordingObserver();

        using var sub = observable.Subscribe(observer);

        resources["$a"] = 2;
        Assert.That(observer.Values, Is.EqualTo(new object?[] { 1, 2 }));

        observable.Dispose();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observer.CompletedCount, Is.EqualTo(1));
            Assert.That(observer.Error, Is.Null);
        }

        resources["$a"] = 3;
        Assert.That(observer.Values, Is.EqualTo(new object?[] { 1, 2 }));
    }

    [Test]
    public void Dispose_DoesNotComplete_Unsubscribed_Observer()
    {
        var (resources, _) = Create();

        resources["$a"] = 1;

        var observable = new ObservableExpression(resources, "$a");

        var o1 = new RecordingObserver();
        var o2 = new RecordingObserver();

        var s1 = observable.Subscribe(o1);
        var s2 = observable.Subscribe(o2);

        s1.Dispose();
        observable.Dispose();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(o1.CompletedCount, Is.EqualTo(0));
            Assert.That(o2.CompletedCount, Is.EqualTo(1));
        }
    }
}