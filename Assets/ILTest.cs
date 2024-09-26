using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

public class ILTest : MonoBehaviour
{
    // Start is called before the first frame update
    delegate int ILAddFunc(int x);
    void Start()
    {
        DynamicMethod res = new DynamicMethod("main", typeof(int), new[]
        {
            typeof(int),
        });
        var gen = res.GetILGenerator();
        gen.Emit(OpCodes.Ldarg_0);
        gen.Emit(OpCodes.Ldc_I4, 1);
        gen.Emit(OpCodes.Add);
        gen.Emit(OpCodes.Ret);

        var f = (ILAddFunc)res.CreateDelegate(typeof(ILAddFunc));
        Debug.Log(f(5));
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
