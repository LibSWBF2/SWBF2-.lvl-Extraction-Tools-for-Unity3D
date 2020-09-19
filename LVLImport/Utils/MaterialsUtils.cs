using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Runtime.InteropServices;
using System.Security;
using System.Runtime.InteropServices.WindowsRuntime;

using UnityEngine;


using LibSWBF2.Types;


static class MaterialsUtils {

	public static bool IsEmissive(uint flags)
    {
        return (flags & (uint)16) != 0; 
    }

    public static bool IsTransparent(uint flags)
    {
        return (flags & (uint)4) != 0;
    }

    public static bool IsScrolling(uint flags)
    {
        return (flags & (uint)16777216) != 0;
    }
}


