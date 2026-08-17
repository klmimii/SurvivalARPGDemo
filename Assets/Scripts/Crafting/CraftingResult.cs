using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//使用Struct而不是class是因为struct零内存垃圾，避免卡顿。class时引用类型，每次调用new CraftResult都会在堆上分配内存。游戏中玩家可能会频繁的去点击合成检测配方，频繁的new对象会导致堆内存不断碰撞从而触发Unity的垃圾回收画面卡顿
//struct类型作为局部变量或方法返回时，他直接分配在栈上，当TryCraft方法执行完毕结束作用域后，栈内存会被瞬间自动收回，无GC增长，对游戏性能非常友好
//而这个CraftResult只是一个数据结果快照，不需要身份标识，只关心他是success=true还是false，不需要被继承，生命周期非常短。从ICraftService。TryCraft返回后，Ui拿到数据弹出提示框，这个对象使命就完成了
/// <summary>
/// 封装合成操作的执行结果
/// </summary>
public readonly struct CraftingResult 
{
    public bool Success { get; }
    public string Message { get; }

    //显式构造函数用来在创建时对两个属性进行初始化
    public CraftingResult(bool success,string message)
    {
        this.Success = success;
        this.Message = message;
    }

    //不需要在外层写new CraftResult(true,"合成成功"）这种比较繁琐的代码，直接调用CraftingResult.Succeed（"合成成功"）
    public static CraftingResult Succeed(string message)
    {
        return new CraftingResult(true, message);
    }

    public static CraftingResult Fail(string message)
    {
        return new CraftingResult(false, message);
    }
}
