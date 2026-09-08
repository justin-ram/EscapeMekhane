using UnityEngine;

public interface IDamage
{
    void takeDamage(int amount, Vector3 damageDirection, int damageSpeed, float pushDurationTimer);


}
