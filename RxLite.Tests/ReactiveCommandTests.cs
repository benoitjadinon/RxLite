using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using System.Diagnostics;
using System.Reactive.Subjects;
using Microsoft.Reactive.Testing;
using System.Linq;

namespace RxLite.Tests
{
    [TestFixture]
    public class ReactiveCommandTests : ReactiveTest
    {
        private static ReactiveCommand<object, object> CreateCommand(
            IObservable<bool> canExecute = null, IScheduler scheduler = null)
        {
            return ReactiveCommand.Create<object, object>(param => param, canExecute, scheduler);
        }

        private static async Task AssertThrowsOnExecuteAsync(ReactiveCommand<Unit, Unit> command, Exception exception)
        {
            command.ThrownExceptions.Subscribe();

            var failed = false;

            try
            {
                await command.Execute();
            }
            catch (Exception ex)
            {
                failed = ex == exception;
            }

            Assert.True(failed);
        }

        private static IObservable<Unit> ThrowAsync(Exception ex)
        {
            return Observable.Throw<Unit>(ex);
        }

        private static IObservable<Unit> ThrowSync(Exception ex)
        {
            throw ex;
        }

        [Test]
        public void CanExecuteExceptionShouldntPermabreakCommands()
        {
            var canExecute = new Subject<bool>();
            var fixture = CreateCommand(canExecute);

            var exceptions = new List<Exception>();
            var canExecuteStates = new List<bool>();
            fixture.CanExecute.Subscribe(canExecuteStates.Add);
            fixture.ThrownExceptions.Subscribe(exceptions.Add);

            canExecute.OnNext(false);
            Assert.False(fixture.CanExecute.FirstAsync().Wait());

            canExecute.OnNext(true);
            Assert.True(fixture.CanExecute.FirstAsync().Wait());

			fixture.Execute().Subscribe();

            canExecute.OnError(new Exception("Aieeeee!"));

            // The command should latch to false forever
            Assert.False(fixture.CanExecute.FirstAsync().Wait());

            Assert.AreEqual(1, exceptions.Count);
            Assert.AreEqual("Aieeeee!", exceptions[0].Message);

            Assert.AreEqual(false, canExecuteStates[canExecuteStates.Count - 1]);
            Assert.AreEqual(true, canExecuteStates[canExecuteStates.Count - 2]);
        }

        [Test]
        public async Task ExecuteAsyncThrowsExceptionOnAsyncError()
        {
            var exception = new Exception("Aieeeee!");

            var command = ReactiveCommand.CreateFromObservable<Unit, Unit>(_ => ThrowAsync(exception));

            await AssertThrowsOnExecuteAsync(command, exception);
        }

        [Test]
        public async Task ExecuteAsyncThrowsExceptionOnError()
        {
            var exception = new Exception("Aieeeee!");

            var command = ReactiveCommand.CreateFromObservable<Unit, Unit>(_ => ThrowSync(exception));

            await AssertThrowsOnExecuteAsync(command, exception);
        }

        [Test]
		public void ExecuteDoesNotThrowOnAsyncError()
		{
			var command = ReactiveCommand.CreateFromObservable<Unit, Unit>(_ => ThrowAsync(new Exception("Aieeeee!")));

		    Exception ex = null;
		    command.ThrownExceptions.Subscribe(x =>
		    {
		        ex = x;
		    });

		    command.Execute().Subscribe(
		        r => Debug.WriteLine(r),
		        e => Debug.WriteLine(e)
		    );

		    Assert.NotNull(ex);
		}

		[Test]
        public void ExecuteDoesNotThrowOnError()
        {
			var command = ReactiveCommand.CreateFromObservable<Unit, Unit>(_ => ThrowSync(new Exception("Aieeeee!")));

			Exception ex = null;

			command.Execute().Subscribe(
				r => Debug.WriteLine(r),
				e => ex = e
			);

			Assert.NotNull(ex);
        }

        [Test]
        public async void MultipleSubscribesShouldntResultInMultipleNotifications()
        {
            var input = new[] { 1, 2, 1, 2 };
            var fixture = CreateCommand();

            var oddList = new List<int>();
            var evenList = new List<int>();
            fixture.Where(x => ((int)x) % 2 != 0).Subscribe(x => oddList.Add((int)x));
            fixture.Where(x => ((int)x) % 2 == 0).Subscribe(x => evenList.Add((int)x));

            foreach (var i in input)
            {
                await fixture.Execute(i);
            }

            Assert.AreEqual(new[] { 1, 1 }, oddList);
            Assert.AreEqual(new[] { 2, 2 }, evenList);
        }

        [Test]
        public void ObservableCanExecuteIsNotNullAfterCanExecuteCalled()
        {
            var fixture = CreateCommand();

            //fixture.CanExecute(null);

            Assert.IsNotNull(fixture.CanExecute);
        }

        [Test]
        public void ReactiveCommand_DefaultCanExecute_IsTrue()
        {
            var command = CreateCommand();
            Assert.IsTrue(command.CanExecute.FirstAsync().Wait());
        }

        [Test]
        public void ReactiveCommand_Excecute_Works()
        {
			var testScheduler = new TestScheduler();
			var command = CreateCommand(scheduler:testScheduler);

			var actual = testScheduler.Start(() => {
				return command.Execute("test");
			});

			actual.Messages.First().Value.Equals("test");
        }
    }
}