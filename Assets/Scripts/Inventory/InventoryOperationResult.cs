using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//为什么是struct而不是class？InventoryOperationResult只是一个临时状态返回对象，用完即丢
//struct是值类型，分配在栈上，完全不会产生垃圾回收，在频繁进行背包增删时，对性能及其友好
//用readonly是因为确保对象一旦被创建出来，期内不得success和Message属性就再也不能被篡改，保证了操作结果的不可变性
/// <summary>
/// 背包操作结果 让背包服务再返回成功/失败的同时能够携带一段解释说明，直接给UI或调用方法使用
/// </summary>
public readonly struct InventoryOperationResult
{
    //采用只有get属性的只读自动属性，外部只能读取结果，不能半路吧Successfalse改成true
    public bool Success { get; }
    public string Message { get; }

    public InventoryOperationResult(bool success,string message)
    {
        Success = success;
        Message = message;
    }

    // 如果不用静态方法，每次返回都要写繁琐的new InventoryOperationResult(true,"拾取成功")
    // 这里用""是因为一般拾取成功不需要提示，直接省略消息
    public static InventoryOperationResult Succeed(string message="")
    {
        return new InventoryOperationResult(true, message);
    }

    //这里不用""是因为拾取失败必须要写明失败原因，背包已满，物品不能堆叠等等
    public static InventoryOperationResult Fail(string message)
    {
        return new InventoryOperationResult(false, message);
    }

}
