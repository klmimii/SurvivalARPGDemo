using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


/// <summary>
/// 数据管理层，维护玩家持有的所有物品格子（容量，格子状态，物品的增删改查与堆叠规则），并抛出event Action Changed通知UI
/// 内部用一个List<ItemStack>记录玩家当前所有的包裹
/// </summary>
public class InventoryModel 
{
    //外部的Presrnter或view可以通过Slots遍历格子去渲染画面,但无法直接篡改底层列表
    private readonly List<ItemStack> slots;
    //只读列表接口，如果不加IReadOnlyList只写List，那么外部代码拿到这个List之后，可以在Model不知情的情况下直接调用InventoryModel.ItemStacks.Clear等，这样Model层的changed事件根本不会触发，数据和UI就会彻底脱节
    //写了之后，外部代码只能遍历读取，不能.Add或Remove。强制所有数据修改必须走InventoryModel内部的AddItem方法，保证数据的绝对安全
    public IReadOnlyList<ItemStack> Slots=>slots;
    //背包容量是多少（能放多少个格子）
    public int Capacity => slots.Count;

    //当背包数据成功变更时，触发。不用UnityAction，因为这是一个纯C#的逻辑类，没有继承Monobehaviour，做到架构层面的解耦
    public event Action Changed;

   public InventoryModel(int capacity)
    {
        //分配capacity个元素的内存空间，列表内部的长度依然是空的
        slots = new List<ItemStack>(capacity);
        //预先填充capacity个new ItemStack。这时slots的count 就永久等于背包容量
        for(int i=0;i<capacity;i++)
        {
            slots.Add(new ItemStack());
        }
    }

    /// <summary>
    /// 用于扣除东西前用CountItem盘点总数/人物界面显示x/10/商店、合成界面
    /// </summary>
    /// <param name="definition"></param>
    /// <returns></returns>
   public int CountItem(ItemDefinition definition)
   {
        int total = 0;
        //一次检查背包里每一个格子
        foreach(ItemStack slot in slots)
        {
            //判断当前格子是不是我们要找的那个物品
            if(slot.Definition==definition)
            {
                //如果是就把这个格子里的书力量加到总数里
                total += slot.Amount;
            }
        }

        //返回最终统计出来的总数（同一个物品可能放在好几个格子里）
        return total;
   }

    /// <summary>
    /// 先模拟算一遍能不能放得下，只有100%能装下时，才真正去改动背包里的数据
    /// </summary>
    /// <param name="definition">要加入什么物品 </param>
    /// <param name="amount">要加入多少个</param>
    /// <returns></returns>
    public bool CanAdd(ItemDefinition definition,int amount)
    {
        //如果传入的物品是空的或者数量小于0，直接判定不能添加
        if (definition == null||amount<=0)
        {
            return false;
        }

        //未使用的空间，还能使用的空间
        int remaining = amount;

        //先计算同类格子中还能剩下多少
        foreach(ItemStack slot in slots)
        {
            if(slot.Definition!=definition)
            {
                continue;
            }

            //计算这个格子还能装多少：最大堆叠上限-当前已装数量 eg要装10个苹果，1号格子有95个，上限100个，装了5个还需要5个，10-（100-95）
            remaining -= definition.maxStack - slot.Amount;
            
            //现有格子就能全装下 提前宣布成功
            if (remaining <= 0)
                return true;
        }

        //再计算空格子总容量(完全利用空闲的空格子)
        foreach(ItemStack slot  in slots)
        {
            //如果不是空格子，跳过
            if (!slot.IsEmpty)
                continue;

            //接上面的例子，还有5个苹果没放，5-100=-95<0，可以放下
            remaining -= definition.maxStack;

            if (remaining <= 0)
                return true;
        }

        //到最后都没有小于0，则返回false，无法放下
        return false;
    }

    /// <summary>
    /// 当CanAdd确认能装下后，TryAdd开始真正修改内存数据
    /// </summary>
    /// <param name="definition"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public bool TryAdd(ItemDefinition definition ,int amount)
    {
        //先调用CanAdd，确认能放下后才执行后面的代码
        //因为有canAdd存在，TryAdd永远都不会出现塞到一半塞不下，导致改了一般数据的尴尬情况，要么100%塞入，要么完全不动背包
        if(!CanAdd(definition,amount))
        {
            return false;
        }

        int remaining = amount;//需要塞入量

        //第一步：优先装满已有的同类格子
        foreach(ItemStack slot in slots )
        {
            //遍历的格子中的物品不是我们想要放入的物品或者当前的格子已经达到maxstack堆叠上限
            if(slot.Definition!=definition||slot.Amount>=definition.maxStack)
            {
                continue;                
            }

            //当前老格子还能添加多少数量物品，老格子剩余空间
            int canAdd = definition.maxStack - slot.Amount;
            //取剩余空间和需要塞入量的最小值
            int addAmount = Math.Min(canAdd, remaining);
            slot.Add(addAmount);
            remaining -= addAmount;

            //如果需要塞入量小于等于0，则放下了，触发广播通知Ui刷新 返回true
            if(remaining<=0)
            {
                Changed?.Invoke();
                return true;
            }

        }

        //第二步，再使用空格子
        foreach(ItemStack slot in slots)
        {
            if(!slot.IsEmpty)
            {
                continue;
            }

            int addAmount = Math.Min(remaining, definition.maxStack);
            slot.Set(definition, addAmount);//给 空格子赋予物品定义和数量
            remaining -= addAmount;

            if(remaining<=0)
            {
                Changed?.Invoke();//塞完了，触发关闭通知UI刷新，返回true
                return true;
            }
        }

        //正常情况下前面的Add以保证不会执行到这里
        return false;
    }

    public bool TryRemove(ItemDefinition definition,int amount)
    {
        //第三个条件是如果背包里拥有的物品数量比要溢出的物品数量还要少的话
        if(definition==null||amount<=0||CountItem(definition)<amount)
        {
            return false;
        }

        int remaining = amount;

        foreach(ItemStack slot in slots)
        {
            if(slot.Definition!=definition)
            {
                continue;
            }

            int removeAmount = Math.Min(slot.Amount, remaining);
            slot.Remove(removeAmount);
            remaining -= removeAmount;

            if(remaining<=0)
            {
                Changed?.Invoke();
                return true;
            }
        }

        return false;

    }

    //修改指定曹魏的物品与数量
    //这个方法只给存档恢复使用，正常游戏中添加，扣除物品必须经过Serveice
    public void SetSlot(int index,ItemDefinition definition,int amount)
    {
        //越界检查
        if(index<0||index>=slots.Count)
        {
            return;
        }
        //如果是空物品或数量小于等于0，则清空槽位
        if(definition ==null||amount<=0)
        {
            slots[index].Clear();
        }

        //如果是有效物品，则设置物品并限制最大堆叠数
        else
        {
            slots[index].Set(definition, Mathf.Min(amount, definition.maxStack));
        }
    }

    public void NotifyChanged()
    {
        Changed?.Invoke();
    }
}
