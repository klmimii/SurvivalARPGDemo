using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using UnityEngine;

public interface IAssetService
{
    Task<T> LoadAsync<T>(string address) where T : UnityEngine.Object;
    void Release(UnityEngine.Object asset);
}
