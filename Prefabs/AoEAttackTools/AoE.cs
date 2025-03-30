using UnityEngine;

public class AoEAttackHitbox : MonoBehaviour
{
    public int damage = 50;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log($"�arp��an nesne: {collision.name}");
        if (collision.CompareTag("Enemy"))
        {
            Debug.Log($"�arp��an nesnenin tag'i: {collision.tag}");

            Enemies enemy = collision.GetComponent<Enemies>();
            if (enemy != null)
            {
                enemy.TakeDamageServerRpc(damage);
                Debug.Log($"{collision.name} AoE sald�r�s�ndan {damage} hasar ald�!");
            }
           
        }
    }

}
