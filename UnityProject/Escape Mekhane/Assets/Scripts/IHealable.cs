public interface IHealable
{
    bool CanReceiveHealing { get; }					// Allows support enemies to ignore dead or fully healed targets.
    void Heal(int amount);					// Restores health without exposing an enemy's private health fields.
}
