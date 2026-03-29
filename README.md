# Abyssbound: AI-Driven ARPG Framework 🌑

**Abyssbound** is a top-down 3D Action RPG framework built in Unity, designed to merge the deep progression and skilling of Old School RuneScape (OSRS) with the fast-paced, loot-driven combat of the Diablo series.

This repository serves as the technical foundation for a modular ARPG ecosystem, focusing on **AI-assisted systems architecture** and **agentic game design**.

## ⚔️ Project Vision
Abyssbound explores a "Dark OSRS" aesthetic—featuring a high-stakes "Gravestone" death system, a massive interconnected open world, and "Elite Keygate" zones inspired by high-end RSPS mechanics.

## 🛠 Technical Architecture
The project is built with a focus on modularity to allow for AI-driven expansion:
- **Physics-Based Movement:** Custom `PlayerController` utilizing `Rigidbody.MovePosition` and `Quaternion.Slerp` for high-precision directional combat.
- **Dynamic Camera System:** Offset-based `CameraFollow` with smoothed target-tracking.
- **Loot Logic:** A scriptable rarity system ranging from Common to **Radiant (Rainbow)** tiers, with base-loot models and boss-specific roll tables.
- **Agent-Ready Foundation:** Designed to interface with LLM-based tools (Claude Code/GPT) for automated system generation (Inventory, Questing, and Stat Scaling).

## 🚀 2026 Roadmap
- [ ] **Phase 1:** Finalize collision-accurate Arena boundaries and basic Enemy AI.
- [ ] **Phase 2:** Implement the ScriptableObject-based Item & Loot Architecture.
- [ ] **Phase 3:** Deploy the "Gravestone" Death System and world-space UI.
- [ ] **Phase 4:** Integrate AI Agent pipelines for procedural world-building and balancing.

## 🏗 Development Philosophy
As a **Certified Scrum Master**, I am managing this project using Agile methodologies. The codebase is structured to be "Agent-Friendly," maintaining strict naming conventions and decoupled logic to facilitate seamless collaboration with AI coding assistants.

---
*Maintained by [Jordon Bradley Wagner](https://github.com/jordan23wagner-ops)*
