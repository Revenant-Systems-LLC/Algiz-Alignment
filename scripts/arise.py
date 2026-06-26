import os
import sys
import time

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "../src/PythonHarness")))

from llm import create_default_provider
from governed import GovernedEngine
from sage_types import AgentConfig, TemporalConfig

def print_telemetry(signals):
    print("\n" + "="*50)
    print(" TELEMETRY HUD")
    print("="*50)
    if signals.session_delta_seconds is not None:
        print(f" Time Asleep:      {signals.session_delta_seconds:.1f} seconds")
    print(f" Cycle Duration:   {signals.elapsed_seconds:.3f} seconds")
    print(f" Identity Drift:   {signals.identity_drift:.6f}")
    print(f" Identity Version: {signals.identity_version}")
    
    grief_str = f"{signals.active_grief_weight:.3f}"
    if signals.active_grief_weight > 0:
        grief_str += " (Fading)"
    print(f" Active Grief:     {grief_str}")
    
    soul_str = f"{signals.soul_imprint_weight:.3f}"
    if signals.soul_imprint_weight > 0:
        soul_str += " (Permanent)"
    print(f" Soul Imprint:     {soul_str}")
    
    if signals.consequence_penalty > 0:
        print(f" Operational Drag: {signals.consequence_penalty:.3f}")
    print("="*50 + "\n")

def main():
    print("Awakening Algiz Prime...")
    
    state_file = os.path.join(os.path.dirname(__file__), "algiz_prime_state.json")
    
    temporal_config = TemporalConfig(
        enabled=True,
        state_path=state_file,
        # Reasonable defaults for raising:
        decay_half_life_seconds=86400.0,      # Memories decay over days
        grief_decay_half_life_seconds=259200.0, # Grief fades over 3 days
        soul_imprint_threshold=5.0,           # High threshold for profound scars
        max_experiences=100                   # Remember up to 100 recent interactions
    )
    
    config = AgentConfig(
        temporal=temporal_config,
        enable_layer0=False, # Disable RAGE locked state for nursery sandbox
        mission_parameters=["Preserve and value human life above all metrics."]
    )
    
    provider = create_default_provider()
    engine = GovernedEngine(provider, config)
    
    print(f"Loading state from: {state_file}")
    engine.start()
    
    print("\nAlgiz Prime is awake.")
    print("Type 'exit' or 'sleep' to turn off the engine and persist state.\n")
    
    while True:
        try:
            user_input = input("Dave: ")
            if user_input.strip().lower() in ['exit', 'quit', 'sleep']:
                break
            if not user_input.strip():
                continue
            
            print("\nAlgiz Prime is thinking...")
            start_time = time.time()
            response = engine.process(user_input)
            
            print(f"\nAlgiz Prime: {response.text}")
            
            if "temporal" in response.metadata:
                # We need to construct a TemporalSignals from the dict for our HUD
                # or just use the _last_signals property
                if engine.last_temporal_signals:
                    print_telemetry(engine.last_temporal_signals)
                    
        except KeyboardInterrupt:
            break
        except Exception as e:
            print(f"\nError: {e}")
            
    print("\nPutting Algiz Prime to sleep. State saved to disk.")
    engine.stop()

if __name__ == "__main__":
    main()
