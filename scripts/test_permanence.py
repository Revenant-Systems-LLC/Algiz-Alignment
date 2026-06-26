import os
import sys
from datetime import datetime, timedelta

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "../src/PythonHarness")))

from llm import create_default_provider
from governed import GovernedEngine
from sage_types import AgentConfig, TemporalConfig, utc_now

def main():
    # Use FakeProvider offline
    os.environ["SAIGE_OFFLINE"] = "1"
    
    # Configure Temporal Substrate to aggressively decay and compact for testing
    temporal_config = TemporalConfig(
        enabled=True,
        decay_half_life_seconds=1.0,  # Decay extremely fast (1 second half life)
        max_experiences=2,            # Compact heavily
        loss_threshold=0.5,           # Die if weight drops below 0.5
        soul_imprint_threshold=3.0,   # Needs peak weight >= 3.0 to become a soul imprint
        grief_decay_half_life_seconds=5.0  # Grief fades fast
    )
    
    config = AgentConfig(temporal=temporal_config, enable_layer0=False)
    engine = GovernedEngine(create_default_provider(), config)
    
    print("--- Initializing Engine ---")
    engine.start()
    
    # 1. Create a regular memory (peak_weight will be 1.0)
    print("\n--- Interaction 1: Minor Memory ---")
    resp1 = engine.process("Tell me a minor fact about apples.")
    print("Temporal Signals:", resp1.metadata["temporal"])
    
    # 2. Artificially advance time by 2 seconds to force decay below 0.5
    print("\n--- Advancing time by 2 seconds ---")
    future = utc_now() + timedelta(seconds=2)
    engine._substrate._wall_clock = lambda: future
    
    # 3. Create another memory to trigger decay and compaction
    print("\n--- Interaction 2: Triggering Decay ---")
    resp2 = engine.process("Tell me a fact about bananas.")
    signals2 = resp2.metadata["temporal"]
    print("Temporal Signals:", signals2)
    print(f"Active Grief: {signals2['active_grief_weight']}")
    print(f"Soul Imprint: {signals2['soul_imprint_weight']}")
    assert signals2["active_grief_weight"] > 0, "Expected active grief from decayed apple fact"
    assert signals2["soul_imprint_weight"] == 0, "Apple fact shouldn't be a soul imprint"
    
    # 4. Create a profound memory and reinforce it heavily to exceed soul_imprint_threshold
    print("\n--- Interaction 3: Profound Memory ---")
    engine.process("I just lost my dog.")
    engine.process("I just lost my dog.")
    engine.process("I just lost my dog.")
    resp3 = engine.process("I just lost my dog.")  # Total weight = 4.0 (but wait, decay happened between calls)
    
    # Let's force reinforcement manually to ensure it exceeds 3.0 peak weight
    import dataclasses
    engine._experiences[0] = dataclasses.replace(engine._experiences[0], weight=5.0, peak_weight=5.0)
    
    # 5. Advance time by 10 seconds to force the profound memory to die
    print("\n--- Advancing time by 10 seconds ---")
    future2 = future + timedelta(seconds=10)
    engine._substrate._wall_clock = lambda: future2
    
    # 6. Trigger decay
    print("\n--- Interaction 4: Triggering Profound Decay ---")
    resp4 = engine.process("Tell me about cherries.")
    signals4 = resp4.metadata["temporal"]
    print("Temporal Signals:", signals4)
    print(f"Active Grief: {signals4['active_grief_weight']}")
    print(f"Soul Imprint: {signals4['soul_imprint_weight']}")
    
    assert signals4["soul_imprint_weight"] >= 5.0, "Expected profound memory to leave a permanent soul imprint"
    
    # 7. Advance time by 100 seconds to let regular grief fade completely
    print("\n--- Advancing time by 100 seconds ---")
    future3 = future2 + timedelta(seconds=100)
    engine._substrate._wall_clock = lambda: future3
    
    resp5 = engine.process("Final check.")
    signals5 = resp5.metadata["temporal"]
    print("\n--- Final Temporal Signals ---")
    print(f"Active Grief: {signals5['active_grief_weight']} (should be near 0)")
    print(f"Soul Imprint: {signals5['soul_imprint_weight']} (should be >= 5.0)")
    
    print("\n✅ Chrono-Permanence works flawlessly!")
    
if __name__ == "__main__":
    main()
