using ExampleProject.Schemes;
using Schema.Runtime;
using UnityEngine;

/// <summary>
/// This is an example script for using Schema during runtime to load game configs from Resources 
/// </summary>
public class Player2DController : MonoBehaviour
{
    [SerializeField]
    private EntitiesEntry playerEntry;
    
    [SerializeField]
    private SpriteRenderer spriteRenderer;
    
    // Start is called before the first frame update
    void Start()
    {
        InitializeConfigs();
    }

    private void InitializeConfigs()
    {
        if (SchemaRuntime.Initialize(OnManifestUpdated).TryErr(out var initError))
        {
            Debug.LogError(initError.Message);
        }
    }

    private void OnManifestUpdated()
    {
        if (!EntitiesScheme.GetEntry(EntitiesScheme.Ids.PLAYER).Try(out var player, 
                out var playerError))
        {
            Debug.LogError(playerError.Message);
        }

        Debug.Log($"Using player entry: {player}");
        playerEntry = player;
        spriteRenderer.sprite = playerEntry.Sprite;
        spriteRenderer.color = playerEntry.Color;
    }

    // Update is called once per frame
    void Update()
    {
        var pos = transform.position;

        var moveDir = new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), 0);
        moveDir = moveDir.normalized * (Time.deltaTime * playerEntry.MoveSpeed);
        
        transform.position = pos + moveDir;
    }
}
