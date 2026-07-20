using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System.Linq;
using M=System.MathF;

public interface IDamageable
{
    public bool MayDamage(int damage);
    public bool TakeDamage(int damage, bool invokeDamageAlert=true, Color? color = null, string label = null);
}

public interface IBeforeDestroy
{
    public void BeforeDestroy();
}

public interface IOnLand
{
    public void OnLandAction();
}

public interface IInteractable
{
    public void OnInteract() {  }
    public void StartInteraction(GameObject target=null) {  }
    public void EndInteraction(GameObject target=null) {  }
    public void ContinuousInteraction(GameObject target=null) {  }
}

public interface IOnMove
{
    public void StartMove();
    public void OnMove();
    public void EndMove();
}

public interface IEquipment
{
    public Unit unit{get;set;}
    public void Apply(Unit unit=null);
    public void Remove();
}

public interface IRecyclable
{
    public void OnRecycle();
}