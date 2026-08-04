using System;
using GameLovers.Services;
using NSubstitute;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	[TestFixture]
	public class MainInstallerTest
	{
		public interface IInterface {}
		public class Implementation : IInterface {}
		public interface IDisposableInterface : IDisposable {}
		public class DisposableImplementation : IDisposableInterface
		{
			public void Dispose() {}
		}

		[TearDown]
		public void Cleanup()
		{
			MainInstaller.Clean();
		}

		[Test]
		// ADMIT: MainInstaller.Resolve<T> delegates to the private static Installer so a bound instance comes back out.
		// RCR: MainInstaller.cs Resolve<T> — return default(T) instead of delegating → RED (AreSame fails). Also reddens
		// the PlayMode bind/resolve tests. 2026-08-02
		public void Bind_Resolve_Successfully()
		{
			var implementation = new Implementation();
			MainInstaller.Bind<IInterface>(implementation);
			
			Assert.AreSame(implementation, MainInstaller.Resolve<IInterface>());
		}

		[Test]
		// ADMIT: MainInstaller.Clean() clears every binding on the private static Installer.
		// RCR: MainInstaller.cs Clean() — drop the delegation → RED (TryResolve still finds the binding). Note: this also
		// disables the fixture's TearDown cleanup. 2026-08-02
		public void Clean_RemovesAllBindings()
		{
			MainInstaller.Bind<IInterface>(new Implementation());
			MainInstaller.Clean();
			
			Assert.IsFalse(MainInstaller.TryResolve<IInterface>(out _));
		}

		[Test]
		// ADMIT: MainInstaller.Clean<T>() delegates the single-interface removal to the private static Installer.
		// RCR: MainInstaller.cs Clean<T>() — skip the delegation → RED (TryResolve still finds the binding). 2026-08-02
		public void CleanGeneric_RemovesSpecificBinding()
		{
			MainInstaller.Bind<IInterface>(new Implementation());
			MainInstaller.Clean<IInterface>();
			
			Assert.IsFalse(MainInstaller.TryResolve<IInterface>(out _));
		}

		[Test]
		// ADMIT: MainInstaller.CleanDispose<T> disposes the bound instance before removing the binding.
		// RCR: MainInstaller.cs CleanDispose<T> — drop the Resolve().Dispose() call → RED (Received(1).Dispose() is never
		// satisfied). 2026-08-02
		public void CleanDispose_CallsDispose()
		{
			var disposable = Substitute.For<IDisposableInterface>();
			MainInstaller.Bind(disposable);
			
			MainInstaller.CleanDispose<IDisposableInterface>();
			
			disposable.Received(1).Dispose();
			Assert.IsFalse(MainInstaller.TryResolve<IDisposableInterface>(out _));
		}

		[Test]
		// ADMIT: MainInstaller.TryResolve forwards to the wrapped Installer rather than reporting success on its own.
		// RCR: MainInstaller.cs TryResolve — `instance = default; return true;` → RED (returns true with nothing bound).
		// The bare `return true;` is NOT a usable mutation: it leaves the `out` parameter unassigned and fails to
		// compile, which the harness reports as a missing report rather than a red.
		public void TryResolve_NotBound_ReturnsFalse()
		{
			Assert.IsFalse(MainInstaller.TryResolve<IInterface>(out _));
		}
	}
}
