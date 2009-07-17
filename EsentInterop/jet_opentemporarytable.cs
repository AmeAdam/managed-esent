﻿//-----------------------------------------------------------------------
// <copyright file="jet_opentemporarytable.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Isam.Esent.Interop.Vista
{
    /// <summary>
    /// The native version of the JET_OPENTEMPORARYTABLE structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NATIVE_OPENTEMPORARYTABLE
    {
        public uint cbStruct;
        public NATIVE_COLUMNDEF* prgcolumndef;
        public uint ccolumn;
        public NATIVE_UNICODEINDEX* pidxunicode;
        public uint grbit;
        public uint* rgcolumnid;
        public uint cbKeyMost;
        public uint cbVarSegMac;
        public IntPtr tableid;
    }

    /// <summary>
    /// A collection of parameters for the JetOpenTemporaryTable method.
    /// </summary>
    public class JET_OPENTEMPORARYTABLE
    {
        /// <summary>
        /// Gets or sets the column definitions for the columns created in
        /// the temporary table.
        /// </summary>
        public JET_COLUMNDEF[] prgcolumndef { get; set; }

        /// <summary>
        /// Gets or sets the number of columns in <see cref="prgcolumndef"/>.
        /// <seealso cref="prgcolumnid"/>
        /// </summary>
        public int ccolumn { get; set; }

        /// <summary>
        /// Gets or sets the locale ID and normalization flags to use to compare any Unicode
        /// key column data in the temporary table. When this parameter is
        /// null, then the default LCID will be used to compare any Unicode key
        /// columns in the temporary table. The default LCID is the U.S. English
        /// locale. When this parameter is null, then the default normalization
        /// flags will be used to compare any Unicode key column data in the temp
        /// table. The default normalization flags are: NORM_IGNORECASE,
        /// NORM_IGNOREKANATYPE, and NORM_IGNOREWIDTH.
        /// </summary>
        public JET_UNICODEINDEX pidxunicode { get; set; }

        /// <summary>
        /// Gets or sets options for the temp table.
        /// </summary>
        public TempTableGrbit grbit { get; set; }

        /// <summary>
        /// Gets or sets the output buffer that receives the array of column
        /// IDs generated during the creation of the temporary table. The
        /// column IDs in this array will exactly correspond to the input array
        /// of column definitions. As a result, the size of this buffer must
        /// correspond to the size of <see cref="prgcolumndef"/>.
        /// </summary>
        public JET_COLUMNID[] prgcolumnid { get; set; }

        /// <summary>
        /// Gets or sets the maximum size for a key representing a given row. The maximum
        /// key size may be set to control how keys are truncated. Key
        /// truncation is important because it can affect when rows are
        /// considered to be distinct. If this parameter is set to 0 or
        /// 255 then the maximum key size and its semantics will remain
        /// identical to the maximum key size supported by Windows Server 2003
        /// and previous releases. This parameter may also be set to a larger
        /// value as a function of the database page size for the instance
        /// <see cref="JET_param.DatabasePageSize"/>. See
        /// <see cref="VistaParam.KeyMost"/> for more information.
        /// </summary>
        public int cbKeyMost { get; set; }

        /// <summary>
        /// Gets or sets maximum amount of data that will be used from any
        /// variable lengthcolumn to construct a key for a given row. This
        /// parameter may be used to control the amount of key space consumed
        /// by any given key column. This limit is in bytes. If this parameter
        /// is zero or is the same as the <see cref="cbKeyMost"/> property
        /// then no limit is in effect.
        /// </summary>
        public int cbVarSegMac { get; set; }

        /// <summary>
        /// Gets the table handle for the temporary table created as a result
        /// of a successful call to JetOpenTemporaryTable.
        /// </summary>
        public JET_TABLEID tableid { get; internal set; }

        /// <summary>
        /// Returns the unmanaged opentemporarytable that represents this managed class.
        /// </summary>
        /// <returns>
        /// A native (interop) version of the JET_OPENTEMPORARYTABLE
        /// </returns>
        internal NATIVE_OPENTEMPORARYTABLE GetNativeOpenTemporaryTable()
        {
            var openTemporaryTable = new NATIVE_OPENTEMPORARYTABLE();
            openTemporaryTable.cbStruct = (uint) Marshal.SizeOf(openTemporaryTable);
            openTemporaryTable.ccolumn = (uint) this.ccolumn;
            openTemporaryTable.grbit = (uint) this.grbit;
            openTemporaryTable.cbKeyMost = (uint) this.cbKeyMost;
            openTemporaryTable.cbVarSegMac = (uint) this.cbVarSegMac;
            return openTemporaryTable;
        }
    }
}