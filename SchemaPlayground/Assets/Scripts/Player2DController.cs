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
    
    // Start is called before the first frame update
    void Start()
    {
        InitializeConfigs();
    }

    private void InitializeConfigs()
    {
        // Call this methdo
        var loadRes = SchemaRuntime.Initialize();

        if (loadRes.Failed)
        {
            Debug.LogError(loadRes.Message);
            return;
        }

        if (!EntitiesScheme.Get().Try(out var entities, out var error))
        {
            Debug.LogError(error.Message);
            return;
        }

        if (!entities.GetEntry(e => e.ID == "Player")
                .Try(out var player, out var entryError))
        {
            Debug.LogError(entryError.Message);
            return;
        }

        Debug.Log($"Using player entry: {player}");
        playerEntry = player;
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
