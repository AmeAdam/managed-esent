﻿//-----------------------------------------------------------------------
// <copyright file="SeekTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.IO;
using Microsoft.Isam.Esent.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InteropApiTests
{
    /// <summary>
    /// Test JetSeek and associated helper methods.
    /// This uses a table with three records in it:
    ///  - 10
    ///  - 20
    ///  - 30
    /// </summary>
    [TestClass]
    public class SeekTests
    {
        /// <summary>
        /// The directory being used for the database and its files.
        /// </summary>
        private string directory;

        /// <summary>
        /// The path to the database being used by the test.
        /// </summary>
        private string database;

        /// <summary>
        /// The name of the table.
        /// </summary>
        private string table;

        /// <summary>
        /// The instance used by the test.
        /// </summary>
        private JET_INSTANCE instance;

        /// <summary>
        /// The session used by the test.
        /// </summary>
        private JET_SESID sesid;

        /// <summary>
        /// Identifies the database used by the test.
        /// </summary>
        private JET_DBID dbid;

        /// <summary>
        /// The tableid being used by the test.
        /// </summary>
        private JET_TABLEID tableid;

        /// <summary>
        /// Columnid of the Long column in the table.
        /// </summary>
        private JET_COLUMNID columnid;

        #region Setup/Teardown

        /// <summary>
        /// Initialization method. Called once when the tests are started.
        /// All DDL should be done in this method.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            this.directory = SetupHelper.CreateRandomDirectory();
            this.database = Path.Combine(this.directory, "database.edb");
            this.table = "table";
            this.instance = SetupHelper.CreateNewInstance(this.directory);

            // turn off logging and temporary tables so initialization is faster
            Api.JetSetSystemParameter(this.instance, JET_SESID.Nil, JET_param.MaxTemporaryTables, 0, null);
            Api.JetSetSystemParameter(this.instance, JET_SESID.Nil, JET_param.Recovery, 0, "off");
            Api.JetInit(ref this.instance);
            Api.JetBeginSession(this.instance, out this.sesid, String.Empty, String.Empty);
            Api.JetCreateDatabase(this.sesid, this.database, String.Empty, out this.dbid, CreateDatabaseGrbit.None);
            Api.JetBeginTransaction(this.sesid);
            Api.JetCreateTable(this.sesid, this.dbid, this.table, 0, 100, out this.tableid);

            var columndef = new JET_COLUMNDEF() { coltyp = JET_coltyp.Long };
            Api.JetAddColumn(this.sesid, this.tableid, "Long", columndef, null, 0, out this.columnid);

            string indexDef = "+long\0\0";
            Api.JetCreateIndex(this.sesid, this.tableid, "primary", CreateIndexGrbit.IndexPrimary, indexDef, indexDef.Length, 100);

            for (int i = 10; i <= 30; i += 10)
            {
                Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
                Api.JetSetColumn(this.sesid, this.tableid, this.columnid, BitConverter.GetBytes(i), 4, SetColumnGrbit.None, null);
                int ignored;
                Api.JetUpdate(this.sesid, this.tableid, null, 0, out ignored);
            }

            Api.JetCloseTable(this.sesid, this.tableid);
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);
            Api.JetOpenTable(this.sesid, this.dbid, this.table, null, 0, OpenTableGrbit.None, out this.tableid);
        }

        /// <summary>
        /// Cleanup after all tests have run.
        /// </summary>
        [TestCleanup]
        public void Teardown()
        {
            Api.JetCloseTable(this.sesid, this.tableid);
            Api.JetEndSession(this.sesid, EndSessionGrbit.None);
            Api.JetTerm(this.instance);
            Directory.Delete(this.directory, true);
        }

        /// <summary>
        /// Verify that the test class has setup the test fixture properly.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void VerifyFixtureSetup()
        {
            Assert.AreNotEqual(JET_INSTANCE.Nil, this.instance);
            Assert.AreNotEqual(JET_SESID.Nil, this.sesid);
            Assert.AreNotEqual(JET_COLUMNID.Nil, this.columnid);

            JET_COLUMNDEF columndef;
            Api.JetGetTableColumnInfo(this.sesid, this.tableid, this.columnid, out columndef);
            Assert.AreEqual(JET_coltyp.Long, columndef.coltyp);
        }

        #endregion Setup/Teardown

        #region Seek tests

        /// <summary>
        /// Test JetSeek(SeekGrbit.SeekLT) when the record isn't found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestJetSeekLTNotFound()
        {
            this.MakeKeyForRecord(10);
            try
            {
                Api.JetSeek(this.sesid, this.tableid, SeekGrbit.SeekLT);
                Assert.Fail("Expected an EsentErrorException");
            }
            catch (EsentErrorException ex)
            {
                Assert.AreEqual(JET_err.RecordNotFound, ex.Error);
            }
        }

        /// <summary>
        /// Test TrySeek(SeekGrbit.SeekLT) when the record isn't found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestTrySeekLTNotFound()
        {
            this.MakeKeyForRecord(10);
            Assert.IsFalse(Api.TrySeek(this.sesid, this.tableid, SeekGrbit.SeekLT));
        }

        /// <summary>
        /// Test JetSeek(SeekGrbit.SeekLT) when the record is found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestJetSeekLT()
        {
            this.MakeKeyForRecord(11);
            Api.JetSeek(this.sesid, this.tableid, SeekGrbit.SeekLT);
            Assert.AreEqual(10, this.GetColumn());
        }

        /// <summary>
        /// Test TrySeek(SeekGrbit.SeekLT) when the record is found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestTrySeekLT()
        {
            this.MakeKeyForRecord(11);
            Assert.IsTrue(Api.TrySeek(this.sesid, this.tableid, SeekGrbit.SeekLT));
            Assert.AreEqual(10, this.GetColumn());
        }

        /// <summary>
        /// Test JetSeek(SeekGrbit.SeekLE) when the record isn't found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestJetSeekLENotFound()
        {
            this.MakeKeyForRecord(9);
            try
            {
                Api.JetSeek(this.sesid, this.tableid, SeekGrbit.SeekLE);
                Assert.Fail("Expected an EsentErrorException");
            }
            catch (EsentErrorException ex)
            {
                Assert.AreEqual(JET_err.RecordNotFound, ex.Error);
            }
        }

        /// <summary>
        /// Test TrySeek(SeekGrbit.SeekLE) when the record isn't found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestTrySeekLENotFound()
        {
            this.MakeKeyForRecord(9);
            Assert.IsFalse(Api.TrySeek(this.sesid, this.tableid, SeekGrbit.SeekLE));
        }

        /// <summary>
        /// Test JetSeek(SeekGrbit.SeekLE) when the record is found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestJetSeekLE()
        {
            this.MakeKeyForRecord(10);
            Assert.AreEqual(JET_wrn.Success, Api.JetSeek(this.sesid, this.tableid, SeekGrbit.SeekLE));
            Assert.AreEqual(10, this.GetColumn());
        }

        /// <summary>
        /// Test TrySeek(SeekGrbit.SeekLE) when the record is found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestTrySeekLE()
        {
            this.MakeKeyForRecord(10);
            Assert.IsTrue(Api.TrySeek(this.sesid, this.tableid, SeekGrbit.SeekLE));
            Assert.AreEqual(10, this.GetColumn());
        }

        /// <summary>
        /// Test JetSeek(SeekGrbit.SeekLE) when the record found is less than the key
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestJetSeekLEFoundLess()
        {
            this.MakeKeyForRecord(11);
            Assert.AreEqual(JET_wrn.SeekNotEqual, Api.JetSeek(this.sesid, this.tableid, SeekGrbit.SeekLE));
            Assert.AreEqual(10, this.GetColumn());
        }

        /// <summary>
        /// Test TrySeek(SeekGrbit.SeekLE) when the record found is less than the key
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestTrySeekLEFoundLess()
        {
            this.MakeKeyForRecord(11);
            Assert.IsTrue(Api.TrySeek(this.sesid, this.tableid, SeekGrbit.SeekLE));
            Assert.AreEqual(10, this.GetColumn());
        }

        /// <summary>
        /// Test JetSeek(SeekGrbit.SeekEQ) when the record isn't found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestJetSeekEQNotFound()
        {
            this.MakeKeyForRecord(19);
            try
            {
                Api.JetSeek(this.sesid, this.tableid, SeekGrbit.SeekEQ);
                Assert.Fail("Expected an EsentErrorException");
            }
            catch (EsentErrorException ex)
            {
                Assert.AreEqual(JET_err.RecordNotFound, ex.Error);
            }
        }

        /// <summary>
        /// Test TrySeek(SeekGrbit.SeekEQ) when the record isn't found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestTrySeekEQNotFound()
        {
            this.MakeKeyForRecord(19);
            Assert.IsFalse(Api.TrySeek(this.sesid, this.tableid, SeekGrbit.SeekEQ));
        }

        /// <summary>
        /// Test JetSeek(SeekGrbit.SeekEQ) when the record is found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestJetSeekEQ()
        {
            this.MakeKeyForRecord(20);
            Assert.AreEqual(JET_wrn.Success, Api.JetSeek(this.sesid, this.tableid, SeekGrbit.SeekEQ));
            Assert.AreEqual(20, this.GetColumn());
        }

        /// <summary>
        /// Test TrySeek(SeekGrbit.SeekEQ) when the record is found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestTrySeekEQ()
        {
            this.MakeKeyForRecord(20);
            Assert.IsTrue(Api.TrySeek(this.sesid, this.tableid, SeekGrbit.SeekEQ));
            Assert.AreEqual(20, this.GetColumn());
        }

        /// <summary>
        /// Test JetSeek(SeekGrbit.SeekGE) when the record isn't found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestJetSeekGENotFound()
        {
            this.MakeKeyForRecord(31);
            try
            {
                Api.JetSeek(this.sesid, this.tableid, SeekGrbit.SeekGE);
                Assert.Fail("Expected an EsentErrorException");
            }
            catch (EsentErrorException ex)
            {
                Assert.AreEqual(JET_err.RecordNotFound, ex.Error);
            }
        }

        /// <summary>
        /// Test TrySeek(SeekGrbit.SeekGE) when the record isn't found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestTrySeekGENotFound()
        {
            this.MakeKeyForRecord(31);
            Assert.IsFalse(Api.TrySeek(this.sesid, this.tableid, SeekGrbit.SeekGE));
        }

        /// <summary>
        /// Test JetSeek(SeekGrbit.SeekGE) when the record is found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestJetSeekGE()
        {
            this.MakeKeyForRecord(30);
            Assert.AreEqual(JET_wrn.Success, Api.JetSeek(this.sesid, this.tableid, SeekGrbit.SeekGE));
            Assert.AreEqual(30, this.GetColumn());
        }

        /// <summary>
        /// Test TrySeek(SeekGrbit.SeekGE) when the record is found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestTrySeekGE()
        {
            this.MakeKeyForRecord(30);
            Assert.IsTrue(Api.TrySeek(this.sesid, this.tableid, SeekGrbit.SeekGE));
            Assert.AreEqual(30, this.GetColumn());
        }

        /// <summary>
        /// Test JetSeek(SeekGrbit.SeekGE) when the record found is greaer than the key
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestJetSeekGEFoundGreater()
        {
            this.MakeKeyForRecord(29);
            Assert.AreEqual(JET_wrn.SeekNotEqual, Api.JetSeek(this.sesid, this.tableid, SeekGrbit.SeekGE));
            Assert.AreEqual(30, this.GetColumn());
        }

        /// <summary>
        /// Test TrySeek(SeekGrbit.SeekGE) when the record found is greater than the key
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestTrySeekGEFoundGreater()
        {
            this.MakeKeyForRecord(29);
            Assert.IsTrue(Api.TrySeek(this.sesid, this.tableid, SeekGrbit.SeekGE));
            Assert.AreEqual(30, this.GetColumn());
        }

        /// <summary>
        /// Test JetSeek(SeekGrbit.SeekGT) when the record isn't found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestJetSeekGTNotFound()
        {
            this.MakeKeyForRecord(30);
            try
            {
                Api.JetSeek(this.sesid, this.tableid, SeekGrbit.SeekGT);
                Assert.Fail("Expected an EsentErrorException");
            }
            catch (EsentErrorException ex)
            {
                Assert.AreEqual(JET_err.RecordNotFound, ex.Error);
            }
        }

        /// <summary>
        /// Test TrySeek(SeekGrbit.SeekGT) when the record isn't found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestTrySeekGTNotFound()
        {
            this.MakeKeyForRecord(30);
            Assert.IsFalse(Api.TrySeek(this.sesid, this.tableid, SeekGrbit.SeekGT));
        }

        /// <summary>
        /// Test JetSeek(SeekGrbit.SeekGT) when the record is found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestJetSeekGT()
        {
            this.MakeKeyForRecord(29);
            Api.JetSeek(this.sesid, this.tableid, SeekGrbit.SeekGT);
            Assert.AreEqual(30, this.GetColumn());
        }

        /// <summary>
        /// Test TrySeek(SeekGrbit.SeekGT) when the record is found
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TestTrySeekGT()
        {
            this.MakeKeyForRecord(29);
            Assert.IsTrue(Api.TrySeek(this.sesid, this.tableid, SeekGrbit.SeekGT));
            Assert.AreEqual(30, this.GetColumn());
        }

        #endregion Seek tests

        #region IndexRange tests

        /// <summary>
        /// Create a descending index range which is empty.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void JetSetIndexRangeDescendingEmpty()
        {
            this.MakeKeyForRecord(10);
            Api.JetSeek(this.sesid, this.tableid, SeekGrbit.SeekLE);

            this.MakeKeyForRecord(10);
            try
            {
                Api.JetSetIndexRange(this.sesid, this.tableid, SetIndexRangeGrbit.None);
                Assert.Fail("Expected an EsentErrorException");
            }
            catch (EsentErrorException ex)
            {
                Assert.AreEqual(JET_err.NoCurrentRecord, ex.Error);
            }
        }

        /// <summary>
        /// Try to create an ascending index range which is empty.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        public void TrySetIndexRangeAscendingEmpty()
        {
            this.MakeKeyForRecord(30);
            Api.JetSeek(this.sesid, this.tableid, SeekGrbit.SeekGE);

            this.MakeKeyForRecord(30);
            Assert.IsFalse(Api.TrySetIndexRange(this.sesid, this.tableid, SetIndexRangeGrbit.RangeUpperLimit));
        }

        #endregion IndexRange tests

        #region Helper Methods

        /// <summary>
        /// Return the value of the columnid of the current record.
        /// </summary>
        /// <returns>The value of the columnid, converted to an int.</returns>
        private int GetColumn()
        {
            var data = new byte[4];
            int actualDataSize;
            Api.JetRetrieveColumn(this.sesid, this.tableid, this.columnid, data, data.Length, out actualDataSize, RetrieveColumnGrbit.None, null);
            Assert.AreEqual(data.Length, actualDataSize);
            return BitConverter.ToInt32(data, 0);
        }

        /// <summary>
        /// Make a key for a record with the given ID
        /// </summary>
        /// <param name="id">The id of the record.</param>
        private void MakeKeyForRecord(int id)
        {
            byte[] data = BitConverter.GetBytes(id);
            Api.JetMakeKey(this.sesid, this.tableid, data, data.Length, MakeKeyGrbit.NewKey);
        }

        #endregion Helper Methods
    }
}
