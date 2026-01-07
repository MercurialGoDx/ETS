public interface ITakeDamageModifier
{
    int Priority { get; }
    float ModifyDamage(float damage);
}