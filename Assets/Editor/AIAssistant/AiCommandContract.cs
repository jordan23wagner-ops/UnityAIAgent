// This file defines the AI Assistant contract types. DO NOT MODIFY without explicit user instruction.
using System;
using System.Collections.Generic;

namespace AIAssistant
{
    public enum ExecutionMode { DryRun, Apply }

        [Serializable]
        public class AiCommandEnvelope {
        public string schemaVersion = "1.0";
        public string requestId = Guid.NewGuid().ToString();
        public ExecutionMode mode = ExecutionMode.DryRun;
        public SceneScope scene = new SceneScope();
        public SafetyScope scope = new SafetyScope {
            allowedRoots = new List<string> {"_AI_WORKSPACE", "_RUNTIME"},
            deniedRoots = new List<string> {"ZONE_1_LOCKED"}
        };
        public AiCommandList commands;
    }

        [Serializable]
        public class SafetyScope {
        public List<string> allowedRoots = new List<string>();
        public List<string> deniedRoots = new List<string>();
        public int maxOperations = 0; // 0 = unlimited
    }

        [Serializable]
        public class AiCommandList {
        public AiCommand[] commands;
    }

    [Serializable]
    public class AiCommand
    {
        // Required
        public string op;

        // Optional per-command scope override (if null, envelope scope can be applied)
        public SafetyScope scope;

        // Common targeting / scene ops
        public string name;
        public string path;        // for find/delete/select/etc
        public string parentPath;  // for createObject parenting

        // ensurePlayerInputStack op payload
        public string playerTag;
        public bool ensureCameraPan = true;

        // validateDropTable op payload
        // validateEnemyDrops op payload
        public string enemyId;
        // validateEnemyPrefabDrops op payload
        public string prefabId;
        public string dropTableId;
        public string[] expectedItems;
        public float minDropChance;

        // validateGate op payload
        public string gateId;
        public string expectedKeyItem;
        public int requiredAmount;

        // runRecipe op payload
        // - recipeName selects a built-in recipe (e.g., "zone1_foundation_validate")
        // - recipeOps optionally overrides the step list (each entry is an op name)
        public string recipeName;
        public string[] recipeOps;
    }

        [Serializable]
        public class AiExecutionResult {
        public bool success = true;
        public ExecutionMode mode;
        public int opsPlanned;
        public int opsExecuted;
        public List<string> errors = new();
        public List<string> warnings = new();
        public List<string> logs = new();
    }

        [Serializable]
        public class SceneScope { public string sceneName; }
}
