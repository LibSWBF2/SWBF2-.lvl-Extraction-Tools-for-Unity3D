using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LibSWBF2;

public abstract class ISWBFClass : MonoBehaviour
{
    public abstract void InitClass(LibSWBF2.Wrappers.EntityClass cl); 
    public abstract void InitInstance(LibSWBF2.Wrappers.Instance inst);
}
