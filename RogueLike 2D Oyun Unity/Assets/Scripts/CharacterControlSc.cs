using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class KarakterHareket : NetworkBehaviour
{
    public float hiz = 1f;
    public float ziplama = 1f;
    public int maxJumpCount = 2;
    public int jumpCounter = 0;

    public int CharHealth;
    public int currentHealth;
    public int attackDamage;
    public int abilityPower;
    public int armor;
    public int gold;
    public string currentWeapon;
    public Image greenHealthBar;
    public Image redHealthBar;

    public Animator characterAnimator;
    private Animator weaponAnimator;
    private Rigidbody2D rb;
    public WeaponsScript weaponsScript;
    public ArmScript armScript;
    public DefaultCharacterStats defaultStats;
    public GameObject weaponObject;
    public float chargeTime = 0.5f;
    public static KarakterHareket instance;

    private bool canAttack = true;
    public float attackSpeed = 1.0f;
    private bool isTakingDamage;

    private NetworkVariable<int> networkHealth = new NetworkVariable<int>();
    private NetworkVariable<int> networkAttackDamage = new NetworkVariable<int>();
    private NetworkVariable<int> networkAbilityPower = new NetworkVariable<int>();
    private NetworkVariable<int> networkArmor = new NetworkVariable<int>();
    private NetworkVariable<int> networkGold = new NetworkVariable<int>();
    private NetworkVariable<int> networkLives = new NetworkVariable<int>(writePerm: NetworkVariableWritePermission.Server);

    public GameObject AoePrefab;

    private Dictionary<ulong, Dictionary<int, float>> playerSkillCooldowns = new Dictionary<ulong, Dictionary<int, float>>();
    private Dictionary<int, float> skillLastUseTime = new Dictionary<int, float>();

    private bool IsOfflineMode()
    {
        bool result = GameManager.Instance != null && GameManager.Instance.isLocalHostMode;
        return result;
    }

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody2D>(); 
        
        characterAnimator = GetComponent<Animator>();

        if (IsOfflineMode() || IsOwner)
        {
            enabled = true;
            Camera playerCamera = GetComponentInChildren<Camera>(true); 
            if (playerCamera != null) 
            { 
                playerCamera.gameObject.SetActive(true);
                AudioListener listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = true;
            } 
            
            if (weaponObject != null) 
            { 
                weaponsScript = weaponObject.GetComponent<WeaponsScript>(); 
                weaponAnimator = weaponObject.GetComponent<Animator>();
            }
        }
        else 
        {   
            enabled = true;
            
            Camera playerCamera = GetComponentInChildren<Camera>(true);
            if (playerCamera != null) 
            {
                 playerCamera.gameObject.SetActive(false); 
                 AudioListener listener = playerCamera.GetComponent<AudioListener>();
                 if (listener != null) listener.enabled = false;
            }
            
            if (!IsServer && rb != null) 
            { 
                rb.isKinematic = true; 
            }
        }

        networkHealth.OnValueChanged += OnHealthChanged;
        networkLives.OnValueChanged += OnLivesChanged;

        if (IsServer)
        {
            ResetToDefaultStats();
            networkHealth.Value = CharHealth;
            networkAttackDamage.Value = attackDamage;
            networkAbilityPower.Value = abilityPower;
            networkArmor.Value = armor;
            networkGold.Value = gold;
        }
        else
        {
            currentHealth = networkHealth.Value; 
        }
       
        UpdateHealthBar();
    }

    void Awake()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                enabled = true;
                characterAnimator = GetComponent<Animator>();
                rb = GetComponent<Rigidbody2D>();
                if (weaponObject != null)
                {
                    weaponsScript = weaponObject.GetComponent<WeaponsScript>();
                    weaponAnimator = weaponObject.GetComponent<Animator>();
                }
                
                if (defaultStats != null)
                {
                    CharHealth = defaultStats.maxHealth;
                    currentHealth = defaultStats.maxHealth;
                    attackDamage = defaultStats.attackDamage;
                    abilityPower = defaultStats.abilityPower;
                    armor = defaultStats.armor;
                    
                }
            }
            else
            {
                Destroy(gameObject);
            }
            return;
        }
    }

    void Start()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            if (greenHealthBar == null || redHealthBar == null)
            {
                GameObject healthBarGreen = GameObject.FindGameObjectWithTag("HealthBarGreen");
                GameObject healthBarRed = GameObject.FindGameObjectWithTag("HealthBarRed");
                
                if (healthBarGreen != null)
                    greenHealthBar = healthBarGreen.GetComponent<Image>();
                
                if (healthBarRed != null)
                    redHealthBar = healthBarRed.GetComponent<Image>();
                
            }
            
            if (defaultStats != null && currentHealth <= 0)
            {
                currentHealth = defaultStats.maxHealth;
                CharHealth = defaultStats.maxHealth;
            }
            
            UpdateHealthBar();
        }
    }

    void Update()
    {
        float hareketInputX = Input.GetAxis("Horizontal");
        bool jumpPressed = Input.GetButtonDown("Jump");
        
        if (!IsOfflineMode() && !IsOwner) 
        {
            return;
        }

        if (Mathf.Abs(hareketInputX) > 0.01f)
        {
            if (characterAnimator != null) characterAnimator.SetBool("isRunning", true);
        }
        else
        {
            if (characterAnimator != null) characterAnimator.SetBool("isRunning", false);
        }
        
        if (jumpPressed)
        {
            if (characterAnimator != null) characterAnimator.SetTrigger("isJumping");
        }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsOfflineMode())
        {
            if (rb != null)
            {
                rb.velocity = new Vector2(hareketInputX * hiz, rb.velocity.y);

                if (jumpPressed && jumpCounter < maxJumpCount)
                {
                    rb.velocity = new Vector2(rb.velocity.x, 0);
                    rb.AddForce(Vector2.up * ziplama, ForceMode2D.Impulse);
                    jumpCounter++;
                }
                
                if (hareketInputX > 0.01f)
                {
                    transform.localScale = new Vector3(1, 1, 1);
                }
                else if (hareketInputX < -0.01f)
                {
                    transform.localScale = new Vector3(-1, 1, 1);
                }
            }
        }
        else
        {
            SubmitMovementInputServerRpc(hareketInputX, jumpPressed);
        }

        HandleAttack(); 
        HandleSkillInput(); 
    }

    void HandleSkillInput()
    {
        if (Input.GetKeyDown(KeyCode.Q)) RequestUseSkillServerRpc(0);
        if (Input.GetKeyDown(KeyCode.E)) RequestUseSkillServerRpc(1);
        if (Input.GetKeyDown(KeyCode.Z)) RequestUseSkillServerRpc(2);
        if (Input.GetKeyDown(KeyCode.C)) RequestUseSkillServerRpc(3);
    }

    [ServerRpc]
    void RequestUseSkillServerRpc(int skillIndex, ServerRpcParams rpcParams = default)
    {

        ulong clientId = rpcParams.Receive.SenderClientId;

        SkillTreeManager manager = FindObjectOfType<SkillTreeManager>();
      

        SkillData skill = manager.skills[skillIndex];


        if (!playerSkillCooldowns.TryGetValue(clientId, out Dictionary<int, float> playerCooldowns))
        {
            playerCooldowns = new Dictionary<int, float>();
            playerSkillCooldowns[clientId] = playerCooldowns;
        }


        playerCooldowns[skillIndex] = Time.time;

        switch (skill.skillType)
        {
            case SkillData.SkillType.ChargeAttack:
                StartCoroutine(ChargeAttackServer(skill));
                break;
            case SkillData.SkillType.AoEAttack:
                 StartCoroutine(AoEAttackServer(skill));
                break;
            case SkillData.SkillType.Dash:
                 StartCoroutine(DashServer(skill));
                break;
            case SkillData.SkillType.Heal:
                 HealServer(skill);
                break;
            default:
                Debug.LogError($"[Server - {clientId}] Unknown SkillType: {skill.skillType}");
                break;
        }

        TriggerSkillCooldownClientRpc(skillIndex, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        });
    }

    [ServerRpc]
    void SubmitMovementInputServerRpc(float hareketInputX, bool jumpPressed)
    {
        if (rb != null)
        {
             rb.velocity = new Vector2(hareketInputX * hiz, rb.velocity.y);

             if (jumpPressed && jumpCounter < maxJumpCount)
             {
                 rb.velocity = new Vector2(rb.velocity.x, 0);
                 rb.AddForce(Vector2.up * ziplama, ForceMode2D.Impulse);
                 jumpCounter++;
             }
            
        }

        if (hareketInputX > 0.01f)
        {
            FlipCharacterClientRpc(1);
        }
        else if (hareketInputX < -0.01f)
        {
            FlipCharacterClientRpc(-1);
        }
    }

    [ClientRpc]
    void FlipCharacterClientRpc(int direction)
    {
        if (transform != null)
        {
            transform.localScale = new Vector3(direction, 1, 1);
        }
    }

    [ServerRpc]
    public void AttackServerRpc(ServerRpcParams rpcParams = default)
    {
        if (weaponObject != null)
        {
            weaponAnimator = weaponObject.GetComponent<Animator>();
          
        }
     
        var clientId = rpcParams.Receive.SenderClientId;


        string animTrigger = weaponsScript.weaponData.normalAttackTrigger;
        if (string.IsNullOrEmpty(animTrigger))
        {
            switch (weaponsScript.weaponData.weaponName)
            {
                case "Sword":
                    animTrigger = "SwordAttack";
                    break;
                case "Sycthe":
                    animTrigger = "ScytheAttack";
                    break;
                case "Hammer":
                    animTrigger = "HammerAttack";
                    break;
                case "Bow":
                    animTrigger = "BowAttack";
                    break;
                default:
                    animTrigger = "Attack";
                    break;
            }
        }

        TriggerAttackAnimationClientRpc(animTrigger);

        canAttack = false;
        StartCoroutine(AttackCooldown());

        string weaponName = weaponsScript.weaponData.weaponName;
        bool isRangedWeapon = weaponName == "Bow" || weaponName == "Pistol" || weaponName == "Rifle";
        
        if (isRangedWeapon)
        {
            HandleRangedWeaponAttack(weaponName, clientId);
        }
        else
        {
            float attackRange = 1.0f;
            float attackRadius = 0.5f;

            Vector2 attackOrigin = (Vector2)transform.position + (Vector2)(transform.right * transform.localScale.x * (attackRange * 0.5f));

            int playerLayer = LayerMask.NameToLayer("Player");
            LayerMask playerLayerMask = 1 << playerLayer;

            Collider2D[] hits = Physics2D.OverlapCircleAll(attackOrigin, attackRadius, playerLayerMask);

            foreach (Collider2D hit in hits)
            {
                if (hit.gameObject == gameObject) continue;

                KarakterHareket targetCharacter = hit.GetComponent<KarakterHareket>();
                if (targetCharacter != null && targetCharacter.IsSpawned)
                {
                    int weaponDamage = weaponsScript.weaponData.damage;
                    int totalDamage = CalculateTotalDamage(weaponDamage);

                    targetCharacter.TakeDamage(totalDamage);
                }
            }
        }
    }

    private void HandleRangedWeaponAttack(string weaponName, ulong clientId)
    {
        GameObject bulletPrefab = null;
        
        if (weaponName == "Pistol" && weaponsScript.pistolPrefab != null)
        {
            bulletPrefab = weaponsScript.pistolBulletPrefab;
        }
        else if (weaponName == "Rifle" && weaponsScript.riflePrefab != null)
        {
            bulletPrefab = weaponsScript.rifleBulletPrefab;
        }
        else if (weaponName == "Bow")
        {
            bulletPrefab = weaponsScript.arrowPrefab;
        }
        
        if (bulletPrefab == null)
        {
            return;
        }
        
        NetworkObject bulletNetworkObj = bulletPrefab.GetComponent<NetworkObject>();
        if (bulletNetworkObj == null)
        {
            return;
        }
        
        Vector3 spawnPosition = weaponObject.transform.position;
        
        Vector3 direction = new Vector3(transform.localScale.x, 0, 0).normalized;
        
        GameObject bullet = Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);
        
        NetworkObject bulletNetObj = bullet.GetComponent<NetworkObject>();
        if (bulletNetObj != null)
        {
            try {
                bulletNetObj.Spawn();
            } 
            catch (System.Exception e) {
                Destroy(bullet);
                return;
            }
        }
        
        Bullet bulletComponent = bullet.GetComponent<Bullet>();
        if (bulletComponent != null)
        {
            bulletComponent.Initialize(direction, CalculateTotalDamage(weaponsScript.weaponData.damage), gameObject);
        }
        else
        {
            Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
            if (bulletRb != null)
            {
                float bulletSpeed = 10f;
                bulletRb.velocity = direction * bulletSpeed;
                
                StartCoroutine(DestroyBulletAfterTime(bullet, 5f));
            }
           
        }
    }

    IEnumerator AttackCooldown()
    {
        float currentAttackSpeed = (weaponsScript != null && weaponsScript.weaponData != null && weaponsScript.weaponData.attackSpeed > 0) 
                                   ? weaponsScript.weaponData.attackSpeed 
                                   : this.attackSpeed; 
        
        float cooldownDuration = currentAttackSpeed;
        
        yield return new WaitForSeconds(cooldownDuration);
        canAttack = true;
    }

    [ClientRpc]
    void TriggerAttackAnimationClientRpc(string triggerName)
    {
        if (weaponAnimator == null && weaponObject != null)
        {
            weaponAnimator = weaponObject.GetComponent<Animator>();
        }
        
        if (weaponAnimator != null) 
        {
            weaponAnimator.SetTrigger(triggerName);
        }
        else
        {
            
            if (weaponsScript != null)
            {
                Animator weaponScriptAnimator = weaponObject.GetComponent<Animator>();
                if (weaponScriptAnimator != null)
                {
                    weaponScriptAnimator.SetTrigger(triggerName);
                }
            }
        }
    }

    void HandleAttack()
    {
        if (!IsOwner) return;

        if (Input.GetMouseButtonDown(0) && canAttack)
        {
             AttackServerRpc();
        }
    }

    public int CalculateTotalDamage(int baseWeaponDamage)
    {
        return baseWeaponDamage + networkAttackDamage.Value;
    }

    private void OnHealthChanged(int previousValue, int newValue)
    {
        currentHealth = newValue;
        
        if (greenHealthBar != null && redHealthBar != null)
        {
            UpdateHealthBar();
        }
        
    }

    private void OnLivesChanged(int previousValue, int newValue)
    {
        
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReceiveDamageServerRpc(int damage, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        if (isTakingDamage || networkHealth.Value <= 0) return;

        int effectiveDamage = Mathf.Max(0, damage - networkArmor.Value);
        networkHealth.Value -= effectiveDamage;
        networkHealth.Value = Mathf.Clamp(networkHealth.Value, 0, CharHealth);


        TriggerHitFeedbackClientRpc();

        if (networkHealth.Value <= 0)
        {
            HandleDeath();
        }
    }

    [ClientRpc]
    void TriggerHitFeedbackClientRpc()
    {
        if (characterAnimator != null) 
        {
            characterAnimator.SetTrigger("Hit"); 
        }
    }

    private void HandleDeath()
    {
        if (!IsServer) return;

        if (networkLives.Value > 0)
        {
            networkLives.Value--;
            RespawnPlayer();
        }
        else
        {
            TriggerPermanentDeathFeedbackClientRpc();
            NetworkObject networkObject = GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.Despawn(true);
            }
        }
    }

    private void RespawnPlayer()
    {
        if (!IsServer) return;

        networkHealth.Value = CharHealth;

        Vector3 spawnPosition = Vector3.zero;
        NetworkPlayerSpawner spawner = FindObjectOfType<NetworkPlayerSpawner>();
        if(spawner != null)
        {   
            spawnPosition = spawner.GetNextSpawnPosition(); 
        } 
        

        if (rb != null) rb.simulated = false;
        transform.position = spawnPosition;
        if (rb != null) 
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = true;
        }

        RespawnFeedbackClientRpc();
    }

    [ClientRpc]
    private void RespawnFeedbackClientRpc()
    {
        
        Collider2D mainCollider = GetComponent<Collider2D>();
        if (mainCollider != null) mainCollider.enabled = true;
        
        if (rb != null) rb.simulated = true; 
        
        this.enabled = true; 

        UpdateHealthBar();

        if (characterAnimator != null)
        {
        }
    }

    [ClientRpc]
    void TriggerPermanentDeathFeedbackClientRpc() 
    {
         if (characterAnimator != null) 
         {
             characterAnimator.SetTrigger("Die"); 
         }
         
    }

    public void StatGuncelle(string statAdi, int deger)
    {
        bool isOnlineMode = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsOfflineMode();
        
        switch (statAdi)
        {
            case "AD":
                attackDamage += deger;
                if (isOnlineMode && IsServer)
                {
                    networkAttackDamage.Value += deger;
                }
                Debug.Log("Yeni AD: " + attackDamage);
                break;

            case "AP":
                abilityPower += deger;
                if (isOnlineMode && IsServer)
                {
                    networkAbilityPower.Value += deger;
                }
                Debug.Log("Yeni AP: " + abilityPower);
                break;

            case "HEALTH":
                CharHealth += deger;
                currentHealth = Mathf.Clamp(currentHealth + deger, 0, CharHealth);
                
                if (isOnlineMode && IsServer)
                {
                    networkHealth.Value = Mathf.Clamp(networkHealth.Value + deger, 0, CharHealth);
                }
                
                UpdateHealthBar();
                Debug.Log("Yeni Sağlık: " + currentHealth + " / " + CharHealth);
                break;

            case "ARMOR":
                armor += deger;
                if (isOnlineMode && IsServer)
                {
                    networkArmor.Value += deger;
                }
                Debug.Log("Yeni Zırh: " + armor);
                break;

            default:
                Debug.LogWarning("Bilinmeyen stat: " + statAdi);
                break;
        }
    }

    public void Die()
    {
        ResetToDefaultStats();
        FindObjectOfType<SaveManager>().SaveGame();
        Debug.Log("Die dan sonra save alındı");
        SceneManager.LoadScene("Level1");
    }
    private void ResetToDefaultStats()
    {
        if (defaultStats != null)
        {
            networkLives.Value = 3;
            gold = defaultStats.gold;
            CharHealth = defaultStats.maxHealth;
            currentHealth = defaultStats.maxHealth;
            attackDamage = defaultStats.attackDamage;
            abilityPower = defaultStats.abilityPower;
            armor = defaultStats.armor;
            currentWeapon = defaultStats.defaultWeapon;

        }
    }

    public void UpdateHealthBar()
    {
       
        bool isOfflineMode = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsOfflineMode();
        
        if (isOfflineMode)
        {
            float healthRatio = (float)currentHealth / CharHealth;
            greenHealthBar.rectTransform.localScale = new Vector3(healthRatio, 1, 1);
            redHealthBar.enabled = currentHealth < CharHealth;
        }
        else
        {
            float healthRatio = (float)networkHealth.Value / CharHealth;
            greenHealthBar.rectTransform.localScale = new Vector3(healthRatio, 1, 1);
            redHealthBar.enabled = networkHealth.Value < CharHealth;
        }
    }

    public void EquipWeapon(WeaponData weaponData)
    {
        if (weaponsScript == null)
        {
            if (weaponObject != null)
            {
                weaponsScript = weaponObject.GetComponent<WeaponsScript>();
                if (weaponsScript == null)
                {
                    return;
                }
                
            }
            else
            {
                return;
            }
        }
        
        weaponsScript.SetWeaponData(weaponData);
        currentWeapon = weaponData.weaponName;
        
        SpriteRenderer weaponRenderer = weaponObject.GetComponent<SpriteRenderer>();
        if (weaponRenderer != null && weaponData.weaponIcon != null)
        {
            weaponRenderer.sprite = weaponData.weaponIcon;
        }
    }

    [ServerRpc]
    public void EquipWeaponServerRpc(string weaponName, int damage, float attackSpeed, int weaponTypeInt, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        WeaponData weaponData = ScriptableObject.CreateInstance<WeaponData>();
        weaponData.weaponName = weaponName;
        weaponData.damage = damage;
        weaponData.attackSpeed = attackSpeed;
        weaponData.weaponType = (WeaponType)weaponTypeInt;
        

        WeaponData[] allWeapons = Resources.FindObjectsOfTypeAll<WeaponData>();
        foreach (WeaponData existingWeapon in allWeapons)
        {
            if (existingWeapon.weaponName == weaponName)
            {
                weaponData.weaponIcon = existingWeapon.weaponIcon;
                weaponData.normalAttackTrigger = existingWeapon.normalAttackTrigger;
                break;
            }
        }
        
        EquipWeaponClientRpc(weaponName, damage, attackSpeed, weaponTypeInt, clientId);
    }
    
    [ClientRpc]
    private void EquipWeaponClientRpc(string weaponName, int damage, float attackSpeed, int weaponTypeInt, ulong ownerClientId)
    {
        if (OwnerClientId == ownerClientId)
        {
            WeaponData weaponData = ScriptableObject.CreateInstance<WeaponData>();
            weaponData.weaponName = weaponName;
            weaponData.damage = damage;
            weaponData.attackSpeed = attackSpeed;
            weaponData.weaponType = (WeaponType)weaponTypeInt;
            
            WeaponData[] allWeapons = Resources.FindObjectsOfTypeAll<WeaponData>();
            foreach (WeaponData existingWeapon in allWeapons)
            {
                if (existingWeapon.weaponName == weaponName)
                {
                    weaponData.weaponIcon = existingWeapon.weaponIcon;
                    weaponData.normalAttackTrigger = existingWeapon.normalAttackTrigger;
                    break;
                }
            }
            
            EquipWeapon(weaponData);
            
            if (!NetworkManager.Singleton.IsListening)
            {
                SaveManager saveManager = FindObjectOfType<SaveManager>();
                if (saveManager != null)
                {
                    saveManager.SaveGame();
                }
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            if (IsServer || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsOfflineMode()) 
            {
                jumpCounter = 0;
            }
        }
    }

    IEnumerator EnemyDamageOverTime(Enemies enemy)
    {
        isTakingDamage = true;
        
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsOfflineMode())
        {
            while (enemy != null && currentHealth > 0 && isTakingDamage)
            {
                int enemyDamage = enemy.enemyStats != null ? enemy.enemyStats.enemyDamage : 0;
                if (enemyDamage > 0)
                {
                    int effectiveDamage = Mathf.Max(0, enemyDamage - armor);
                    currentHealth -= effectiveDamage;
                    currentHealth = Mathf.Clamp(currentHealth, 0, CharHealth);
                    
                    if (characterAnimator != null)
                    {
                        characterAnimator.SetTrigger("Hit");
                    }
                    
                    UpdateHealthBar();
                    
                    if (currentHealth <= 0)
                    {
                        Die();
                        break;
                    }
                }
                
                float enemyAttackSpeed = (enemy.enemyStats != null && enemy.enemyStats.attackSpeed > 0) ? enemy.enemyStats.attackSpeed : 1.0f;
                yield return new WaitForSeconds(1f / enemyAttackSpeed);
            }
        }
        else 
        {
            while (enemy != null && networkHealth.Value > 0 && isTakingDamage)
            {
                if (IsServer) 
                {
                    int enemyDamage = enemy.enemyStats != null ? enemy.enemyStats.enemyDamage : 0;
                    if (enemyDamage > 0)
                    {
                        ReceiveDamageServerRpc(enemyDamage);
                    }
                }
                else
                {
                    isTakingDamage = false;
                    break; 
                }
                
                float enemyAttackSpeed = (enemy.enemyStats != null && enemy.enemyStats.attackSpeed > 0) ? enemy.enemyStats.attackSpeed : 1.0f;
                yield return new WaitForSeconds(1f / enemyAttackSpeed);
            }
        }
        
        isTakingDamage = false;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            Enemies enemy = collision.gameObject.GetComponent<Enemies>();
            if (enemy != null && !isTakingDamage)
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsOfflineMode())
                {
                    int enemyDamage = enemy.enemyStats != null ? enemy.enemyStats.enemyDamage : 0;
                    if (enemyDamage > 0 && !isTakingDamage)
                    {
                        isTakingDamage = true;
                        TakeDamage(enemyDamage);
                        
                        StartCoroutine(SinglePlayerDamageCooldown(enemy.enemyStats != null ? 
                            1f / enemy.enemyStats.attackSpeed : 1f));
                    }
                }
                else
                {
                    StartCoroutine(EnemyDamageOverTime(enemy));
                }
            }
        }
    }
    
    private IEnumerator SinglePlayerDamageCooldown(float interval)
    {
        yield return new WaitForSeconds(interval);
        isTakingDamage = false;
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            isTakingDamage = false;
        }
    }

    IEnumerator DestroyBulletAfterTime(GameObject bullet, float time)
    {
        yield return new WaitForSeconds(time);
        
        if (bullet != null)
        {
            NetworkObject bulletNetObj = bullet.GetComponent<NetworkObject>();
            if (bulletNetObj != null && bulletNetObj.IsSpawned)
            {
                bulletNetObj.Despawn();
            }
            else
            {
                Destroy(bullet);
            }
        }
    }

    private IEnumerator ChargeAttackServer(SkillData skill)
    {
        if (weaponsScript == null || weaponsScript.weaponData == null)
        {
            yield break;
        }

        string animTrigger = weaponsScript.weaponData.normalAttackTrigger;
        if (string.IsNullOrEmpty(animTrigger))
        {
            switch (weaponsScript.weaponData.weaponName)
            {
                case "Sword":
                    animTrigger = "SwordAttack";
                    break;
                case "Sycthe":
                    animTrigger = "ScytheAttack";
                    break;
                case "Hammer":
                    animTrigger = "HammerAttack";
                    break;
                case "Bow":
                    animTrigger = "BowAttack";
                    break;
                default:
                    animTrigger = "Attack";
                    break;
            }
        }

        TriggerAttackAnimationClientRpc(animTrigger);

        StopCoroutine(AttackCooldown());
        canAttack = true;

        string weaponName = weaponsScript.weaponData.weaponName;
        bool isRangedWeapon = weaponName == "Bow" || weaponName == "Pistol" || weaponName == "Rifle";

        if (isRangedWeapon)
        {
            GameObject bulletPrefab = null;
            
            if (weaponName == "Pistol" && weaponsScript.pistolPrefab != null)
            {
                bulletPrefab = weaponsScript.pistolBulletPrefab;
            }
            else if (weaponName == "Rifle" && weaponsScript.riflePrefab != null)
            {
                bulletPrefab = weaponsScript.rifleBulletPrefab;
            }
            else if (weaponName == "Bow")
            {
                bulletPrefab = weaponsScript.arrowPrefab;
            }

            if (bulletPrefab != null)
            {
                Vector3 spawnPosition = weaponObject.transform.position;
                Vector3 direction = new Vector3(transform.localScale.x, 0, 0).normalized;
                GameObject bullet = Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);
                
                NetworkObject bulletNetObj = bullet.GetComponent<NetworkObject>();
                if (bulletNetObj != null)
                {
                    try 
                    {
                        bulletNetObj.Spawn();
                        
                        Bullet bulletComponent = bullet.GetComponent<Bullet>();
                        if (bulletComponent != null)
                        {
                            int skillDamage = CalculateTotalDamage(weaponsScript.weaponData.damage) * 2;
                            bulletComponent.Initialize(direction, skillDamage, gameObject);
                        }
                    }
                    catch (System.Exception e)
                    {
                        Destroy(bullet);
                    }
                }
            }
        }
        else
        {
            float attackRange = 1.0f;
            float attackRadius = 0.5f;
            Vector2 attackOrigin = (Vector2)transform.position + (Vector2)(transform.right * transform.localScale.x * (attackRange * 0.5f));

            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            LayerMask targetLayers = (1 << playerLayer) | (1 << enemyLayer);

            Collider2D[] hits = Physics2D.OverlapCircleAll(attackOrigin, attackRadius, targetLayers);

            foreach (Collider2D hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                
                KarakterHareket playerTarget = hit.GetComponent<KarakterHareket>();
                if (playerTarget != null && playerTarget.IsSpawned && playerTarget != this)
                {
                    int weaponDamage = weaponsScript.weaponData.damage;
                    int totalDamage = CalculateTotalDamage(weaponDamage) * 2; 
                    playerTarget.ReceiveDamageServerRpc(totalDamage);
                    continue;
                }

                Enemies enemyTarget = hit.GetComponent<Enemies>();
                if (enemyTarget != null && enemyTarget.IsSpawned)
                {
                    int weaponDamage = weaponsScript.weaponData.damage;
                    int totalDamage = CalculateTotalDamage(weaponDamage) * 2; 
                    enemyTarget.TakeDamageServerRpc(totalDamage);
                }
            }
        }

        yield return new WaitForSeconds(skill.cooldown);
    }

    private IEnumerator AoEAttackServer(SkillData skill)
    {
        GameObject aoeInstance = Instantiate(AoePrefab, transform.position, Quaternion.identity);
        NetworkObject netObj = aoeInstance.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true);
            
            int skillDamage = (weaponsScript != null && weaponsScript.weaponData != null) 
                ? (int)(weaponsScript.weaponData.damage * skill.damageMultiplier) 
                : (int)(attackDamage * skill.damageMultiplier);
            
            float aoeRadius = 3.0f;
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, aoeRadius);
            
            foreach (Collider2D hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                
                Enemies enemyTarget = hit.GetComponent<Enemies>();
                if (enemyTarget != null && enemyTarget.IsSpawned)
                {
                    enemyTarget.TakeDamageServerRpc(skillDamage);
                    continue;
                }
                
                KarakterHareket playerTarget = hit.GetComponent<KarakterHareket>();
                if (playerTarget != null && playerTarget.IsSpawned && playerTarget != this)
                {
                    playerTarget.TakeDamage(skillDamage);
                }
            }
            
            yield return new WaitForSeconds(skill.chargeTime);
            
            if (netObj != null && netObj.IsSpawned)
            {
                 netObj.Despawn();
            }
            else
            {
                 Destroy(aoeInstance);
            }
        }
        else
        {
             Destroy(aoeInstance);
        }
    }

    private IEnumerator DashServer(SkillData skill)
    {
        if (rb == null) yield break;
        
        float dashDirection = transform.localScale.x > 0 ? 1f : -1f;
        float dashForce = 60f;
        float dashDuration = 0.4f;
        int dashDamage = 15;
        float dashDistance = 5f;

        int originalLayer = gameObject.layer;
        gameObject.layer = LayerMask.NameToLayer("Invulnerable");

        Vector2 originalVelocity = rb.velocity;
        rb.velocity = Vector2.zero;

        Vector2 targetPosition = (Vector2)transform.position + Vector2.right * dashDirection * dashDistance;

        float elapsedTime = 0f;
        Vector2 startPosition = transform.position;
        
        while (elapsedTime < dashDuration)
        {
            float t = elapsedTime / dashDuration;
            t = Mathf.SmoothStep(0, 1, t);
            transform.position = Vector2.Lerp(startPosition, targetPosition, t);

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.8f);
            foreach(var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                
                Enemies enemy = hit.GetComponent<Enemies>();
                if (enemy != null && enemy.IsSpawned)
                {
                    enemy.TakeDamageServerRpc(dashDamage);
                    continue;
                }
                
                KarakterHareket playerTarget = hit.GetComponent<KarakterHareket>();
                if (playerTarget != null && playerTarget.IsSpawned && playerTarget != this)
                {
                    playerTarget.ReceiveDamageServerRpc(dashDamage);
                }
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        gameObject.layer = originalLayer;
        
        rb.velocity = originalVelocity;
    }

    private void HealServer(SkillData skill)
    {
        int healAmount = Mathf.Max(20, (int)skill.damageMultiplier);
        
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsOfflineMode())
        {
            currentHealth = Mathf.Clamp(currentHealth + 300, 0, CharHealth);
            UpdateHealthBar();
        }
        else
        {
            networkHealth.Value = Mathf.Clamp(networkHealth.Value + 300, 0, CharHealth);
            
            TriggerHealEffectClientRpc(OwnerClientId);
            
            float healRadius = 5.0f;
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, healRadius);
            
            foreach (Collider2D hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                
                KarakterHareket playerTarget = hit.GetComponent<KarakterHareket>();
                if (playerTarget != null && playerTarget.IsSpawned)
                {
                    
                    playerTarget.networkHealth.Value = Mathf.Clamp(playerTarget.networkHealth.Value + healAmount, 0, playerTarget.CharHealth);
                    TriggerHealEffectClientRpc(playerTarget.OwnerClientId);
                }
            }
        }
    }
    
    [ClientRpc]
    private void TriggerHealEffectClientRpc(ulong targetClientId)
    {
        
        if (OwnerClientId == targetClientId)
        {
            if (greenHealthBar != null && redHealthBar != null)
            {
                UpdateHealthBar();
            }
        }
    }

    [ClientRpc]
    void TriggerSkillCooldownClientRpc(int skillIndex, ClientRpcParams clientRpcParams = default)
    {
        SkillTreeManager manager = FindObjectOfType<SkillTreeManager>();
        
        if (manager != null && skillIndex >= 0 && skillIndex < manager.skillIcons.Count)
        {
            if (skillIndex < manager.skills.Count)
            {
                 SkillData skill = manager.skills[skillIndex];
                 manager.skillIcons[skillIndex]?.StartCooldown(skill.cooldown);
            }
        }
    }

    public void TakeDamage(int damage)
    {
        
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsOfflineMode())
        {
            int effectiveDamage = Mathf.Max(0, damage - armor);
            currentHealth -= effectiveDamage;
            currentHealth = Mathf.Clamp(currentHealth, 0, CharHealth);
            
            if (characterAnimator != null)
            {
                characterAnimator.SetTrigger("Hit");
            }
            
            UpdateHealthBar();
            
            if (currentHealth <= 0)
            {
                Die();
            }
        }
        else
        {
            ReceiveDamageServerRpc(damage);
        }
    }
}

