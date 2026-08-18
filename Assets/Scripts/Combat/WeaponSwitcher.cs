using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponSwitcher : MonoBehaviour
{
    [SerializeField] private WeaponController weaponController;//武器控制引用
    [SerializeField] private WeaponDefinition dagger;//匕首配置
    [SerializeField] private WeaponDefinition bow;//弓箭配置
    [SerializeField] private WeaponDefinition longBlade;//长剑配置
    [SerializeField] private InputActionReference equipDaggerAction;//快捷键
    [SerializeField] private InputActionReference equipBowAction;//
    [SerializeField] private InputActionReference equipLongBladeAction;
    [SerializeField] private ToastView toastView;//UI消息提示组件

    private void OnEnable()
    {
        equipDaggerAction.action.Enable();
        equipBowAction.action.Enable();
        equipLongBladeAction.action.Enable();

        equipDaggerAction.action.performed += OnEquipDagger;
        equipBowAction.action.performed += OnEquipBow;
        equipLongBladeAction.action.performed += OnEquipLongBlade;
    }

    private void OnDisable()
    {
        equipDaggerAction.action.performed -= OnEquipDagger;
        equipBowAction.action.performed -= OnEquipBow;
        equipLongBladeAction.action.performed -= OnEquipLongBlade;

        equipDaggerAction.action.Disable();
        equipBowAction.action.Disable();
        equipLongBladeAction.action.Disable();
    }

    private void OnEquipDagger(InputAction.CallbackContext context)
    {
        TryEquip(dagger);
    }

    private void OnEquipBow(InputAction.CallbackContext context)
    {
        TryEquip(bow);
    }

    private void OnEquipLongBlade(InputAction.CallbackContext context)
    {
        TryEquip(longBlade);
    }

    private void TryEquip(WeaponDefinition weapon)
    {
        if (weapon == null || (GameBootstrap.InputMode != null && !GameBootstrap.InputMode.IsGameplay()))
        {
            return;
        }

        weaponController.Equip(weapon);
        if (toastView != null)
        {
            toastView.Show($"切换武器：{weapon.displayName}");
        }
    }
}
