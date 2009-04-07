﻿//-----------------------------------------------------------------------
// <copyright file="WeakReferenceOfTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.
// </copyright>
//-----------------------------------------------------------------------

using System;
using Microsoft.Isam.Esent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PixieTests
{
    [TestClass]
    public class WeakReferenceOfTests
    {
        [TestMethod]
        [Priority(0)]
        public void VerifyIdReturnsObjectId()
        {
            var obj = new ManagedDisposableObject();
            var weakref = new WeakReferenceOf<ManagedDisposableObject>(obj);
            Assert.AreEqual(obj.Id, weakref.Id);
        }

        [TestMethod]
        [Priority(0)]
        public void VerifyIdReturnsObjectIdWhenObjectIsFinalized()
        {
            var obj = new ManagedDisposableObject();
            DisposableObjectId id = obj.Id;
            var weakref = new WeakReferenceOf<ManagedDisposableObject>(obj);
            obj = null;
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.AreEqual(id, weakref.Id);
        }

        [TestMethod]
        [Priority(0)]
        public void VerifyIsAliveReturnsTrueWhenObjectIsAlive()
        {
            var obj = new ManagedDisposableObject();
            var weakref = new WeakReferenceOf<ManagedDisposableObject>(obj);
            Assert.IsTrue(weakref.IsAlive);
        }

        [TestMethod]
        [Priority(0)]
        public void VerifyIsAliveReturnsFalseWhenObjectIsGarbageCollected()
        {
            var weakref = new WeakReferenceOf<ManagedDisposableObject>(new ManagedDisposableObject());
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.IsFalse(weakref.IsAlive);
        }

        [TestMethod]
        [Priority(0)]
        public void VerifyTargetReturnsObjectWhenObjectIsAlive()
        {
            var obj = new ManagedDisposableObject();
            var weakref = new WeakReferenceOf<ManagedDisposableObject>(obj);
            Assert.AreEqual(obj, weakref.Target);
        }

        [TestMethod]
        [Priority(0)]
        public void VerifyTargetReturnsNullWhenObjectIsGarbageCollected()
        {
            var weakref = new WeakReferenceOf<ManagedDisposableObject>(new ManagedDisposableObject());
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.AreEqual(null, weakref.Target);
        }
    }
}