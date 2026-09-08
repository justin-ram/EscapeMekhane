using UnityEngine;

[CreateAssetMenu]
public class gunStats : ScriptableObject
{
    public GameObject gunModel;

    [Range(1, 100)][SerializeField] public int shootDamage;
    [Range(1, 100)][SerializeField] public int shootDistance;
    [Range(0.1f, 2)][SerializeField] public float shootFireRate;

    public Vector3 damageDir;
    [SerializeField] public int pushBackSpeed;
    [SerializeField] public float pushBackDuration;
    public int ammoCur;

    [Range(5, 50)] public int ammoMax;

    public ParticleSystem hitEffect;
    public AudioClip[] shootSound;
    [Range(0, 1)] public float shootSoundVol;

    [Header("Projectile Guns")]
    [SerializeField] public bool isProjectile;
    public GameObject projectile;

    [SerializeField] public int weaponPushSpeed;
    [SerializeField] public float weaponPushLastsTimer;
     public float weaponPushLasts;
}
