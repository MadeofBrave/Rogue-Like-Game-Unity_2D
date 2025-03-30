using System.Collections;
using UnityEngine;

public class AttackRange : MonoBehaviour
{
    public Transform player;
    public GameObject enemy;
    public float moveSpeed = 1.0f;
    private float originalRadius;
    public CircleCollider2D circleCollider;
    public EnemyStats stats;
    private Animator animator;
    public KarakterHareket karakterHareket;
    private bool isAttacking = false;
    private bool isFacingRight;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player == null)
        {
            Debug.LogError("Player bulunamad�! L�tfen sahnede 'Player' tag'ine sahip bir GameObject oldu�undan emin olun.");
        }

        if (enemy == null)
        {
            enemy = this.gameObject;
        }

        if (enemy != null)
        {
            stats = enemy.GetComponent<Enemies>()?.enemyStats;

            if (stats == null)
            {
                Debug.LogError("EnemyStats bulunamad�! Enemies script'inin eklendi�inden emin olun.");
            }
        }

        animator = enemy.GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError("Animator bile�eni bulunamad�! L�tfen d��man nesnesinde Animator oldu�undan emin olun.");
        }

        if (circleCollider == null)
        {
            circleCollider = GetComponent<CircleCollider2D>();
            if (circleCollider == null)
            {
                Debug.LogError("CircleCollider2D atanamad�! L�tfen bu bile�eni nesneye ekleyin veya Inspector'dan atay�n.");
            }
        }

        if (karakterHareket == null)
        {
            karakterHareket = player?.GetComponent<KarakterHareket>();
            if (karakterHareket == null)
            {
                Debug.LogError("KarakterHareket bulunamad�! Player nesnesinde KarakterHareket script'inin oldu�undan emin olun.");
            }
        }
    }



    void Update()
    {

        if (Vector2.Distance(new Vector2(player.position.x, player.position.y),new Vector2(enemy.transform.position.x, enemy.transform.position.y)) <= stats.attackRange)
        {
            MoveTowardsPlayer();
        }

    }

    public void MoveTowardsPlayer()
    {
        
        float distance = Vector2.Distance(enemy.transform.position, player.position);

        if (player.position.x > enemy.transform.position.x && enemy.transform.localScale.x > 0)
        {        
            Flip();
        }
        else if (player.position.x < enemy.transform.position.x && enemy.transform.localScale.x < 0)
        {
            Flip();
        }

        if (distance > 1.1f)
        {
            //Debug.Log((distance));
            Vector2 direction = (player.position - enemy.transform.position).normalized;
            enemy.transform.Translate(direction * stats.moveSpeed * Time.deltaTime);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            StartCoroutine(Attack());
        }
        if (collision.gameObject.layer == LayerMask.NameToLayer("Weapons"))
        {
            return;
        }
    }

    private IEnumerator Attack()
    {
        if (!isAttacking)
        {
            isAttacking = true;

            if (karakterHareket != null && stats != null)
            {
                karakterHareket.TakeDamage(stats.enemyDamage);
                Debug.Log("Enemy sald�rd�! Hasar: " + stats.enemyDamage);
                animator.SetTrigger("Attack");
            }
            else
            {
                Debug.LogError("KarakterHareket veya Stats referans� null!");
            }

            yield return new WaitForSeconds(stats.attackSpeed);

            isAttacking = false;
        }
    }


    public void Flip()
    {
        Transform canvasTransform = enemy.transform.Find("Canvas");
        if (canvasTransform != null)
        {
            Vector3 canvasScale = canvasTransform.localScale;
            canvasScale.x = Mathf.Abs(canvasScale.x); 
            canvasTransform.localScale = canvasScale;
        }
        isFacingRight = !isFacingRight;
        Vector3 localScale = enemy.transform.localScale;
        localScale.x *= -1;
        enemy.transform.localScale = localScale;
    }
    /*IEnumerator AdjustColliderSize()
    {
        // Collider'� b�y�t
        circleCollider.radius = 0.2f;

        // Animasyon s�resi kadar bekle
        yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);

        // Collider'� eski haline d�nd�r
        circleCollider.radius = originalRadius;
    */
}
