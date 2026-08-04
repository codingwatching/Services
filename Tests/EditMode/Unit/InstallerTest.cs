using System;
using GameLovers.Services;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	public class InstallerTest
	{
		private interface IInterface {}
		private interface IInterface2 {}
		private interface IInterface3 {}
		private class Implementation : IInterface {}
		private class MultiImpl : IInterface, IInterface2 {}
		private class TripleImpl : IInterface, IInterface2, IInterface3 {}

		private Installer _installer;
		
		[SetUp]
		public void Init()
		{
			_installer = new Installer();
		}

		[Test]
		// ADMIT: Installer.Resolve casts and returns the bound instance for the requested interface.
		// RCR: Installer.cs Resolve — return default(T) instead of the bound instance → RED (Assert.IsNotNull fails). Also
		// reddens the multi-interface and Clean tests. 2026-08-02
		public void Bind_Resolve_Successfully()
		{
			_installer.Bind<IInterface>(new Implementation());
			
			var instance = _installer.Resolve<IInterface>();
			
			Assert.IsNotNull(instance);
			Assert.AreSame(typeof(Implementation), instance.GetType());
		}

		[Test]
		// ADMIT: Installer.Bind rejects a non-interface type parameter because the registry is keyed by interface.
		// RCR: Installer.cs Bind<T> — return `this` instead of throwing on a non-interface → RED (no ArgumentException).
		// 2026-08-02
		public void Bind_NotInterface_ThrowsException()
		{
			Assert.Throws<ArgumentException>(() => _installer.Bind(new Implementation()));
		}

		[Test]
		// ADMIT: Installer.Resolve throws ArgumentException for an unbound interface rather than returning null.
		// RCR: Installer.cs Resolve — return default(T) from the missing-binding branch → RED (no ArgumentException). Also
		// reddens Clean_Generic_RemovesOnlyBoundInterface. 2026-08-02
		public void Resolve_NotBinded_ThrowsException()
		{
			Assert.Throws<ArgumentException>(() => _installer.Resolve<IInterface>());
		}

		[Test]
		// ADMIT: Installer.Bind<T, T1, T2> binds the instance under the second interface as well as the first.
		// RCR: Installer.cs Bind<T,T1,T2> — bind null for T2 → RED (Resolve<IInterface2>() returns null, AreSame fails).
		// 2026-08-02
		public void Bind_MultiInterface_ResolveBothInterfaces()
		{
			var instance = new MultiImpl();
			_installer.Bind<MultiImpl, IInterface, IInterface2>(instance);

			Assert.AreSame(instance, _installer.Resolve<IInterface>());
			Assert.AreSame(instance, _installer.Resolve<IInterface2>());
		}

		[Test]
		// ADMIT: Installer.Bind<T, T1, T2, T3> binds the instance under the third interface as well.
		// RCR: Installer.cs Bind<T,T1,T2,T3> — bind null for T3 → RED (Resolve<IInterface3>() returns null, AreSame
		// fails). 2026-08-02
		public void Bind_TripleInterface_ResolveAllInterfaces()
		{
			var instance = new TripleImpl();
			_installer.Bind<TripleImpl, IInterface, IInterface2, IInterface3>(instance);

			Assert.AreSame(instance, _installer.Resolve<IInterface>());
			Assert.AreSame(instance, _installer.Resolve<IInterface2>());
			Assert.AreSame(instance, _installer.Resolve<IInterface3>());
		}

		[Test]
		// ADMIT: Installer.TryResolve outs the bound instance, not just the found/not-found flag.
		// RCR: Installer.cs TryResolve — out default(T) instead of the cast instance → RED (AreSame(instance, bound) fails
		// while the bool assertions still pass). 2026-08-02
		public void TryResolve_DirectInvocation_OutsValueWhenBound()
		{
			var instance = new Implementation();
			_installer.Bind<IInterface>(instance);

			var resolved = _installer.TryResolve<IInterface>(out var bound);
			var notFound = _installer.TryResolve<IInterface2>(out var unbound);

			Assert.IsTrue(resolved);
			Assert.AreSame(instance, bound);
			Assert.IsFalse(notFound);
			Assert.IsNull(unbound);
		}

		[Test]
		// ADMIT: Installer.Clean<T> removes the binding for exactly the requested interface.
		// RCR: Installer.cs Clean<T> — skip the removal → RED (Resolve<IInterface>() still succeeds, Assert.Throws fails).
		// 2026-08-02
		public void Clean_Generic_RemovesOnlyBoundInterface()
		{
			var first = new Implementation();
			var second = new MultiImpl();
			_installer.Bind<IInterface>(first);
			_installer.Bind<IInterface2>(second);

			_installer.Clean<IInterface>();

			Assert.Throws<ArgumentException>(() => _installer.Resolve<IInterface>());
			Assert.AreSame(second, _installer.Resolve<IInterface2>());
		}
	}
}