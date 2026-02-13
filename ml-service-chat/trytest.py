import httpx
import json

def test_pocketree():
    url = "http://127.0.0.1:8080/chat"
    
    # Test queries
    payloads = [
        {"user_id": "team_tester", "message": "talk to support"},
        {"user_id": "team_tester", "message": "hello"},
        {"user_id": "team_tester", "message": "George"},
        {"user_id": "team_tester", "message": "I would like to find out more about sustainability"},
        {"user_id": "team_tester", "message": "How do I recycle plastic in Singapore?"},
        {"user_id": "team_tester", "message": "What is the Green Plan 2030?"},
        {"user_id": "team_tester", "message": "What can i do today?"}
    ]

    print("--- PockeTree Connection Test ---")
    with httpx.Client(timeout=30.0) as client:
        for p in payloads:
            print(f"\nSending: {p['message']}")
            try:
                response = client.post(url, json=p)
                if response.status_code == 200:
                    print(f"Bot: {response.json()['response']}")
                else:
                    print(f"Error {response.status_code}: {response.text}")
            except Exception as e:
                print(f"Connection failed: {e}")
                print("Make sure your main script is running on port 8080!")

if __name__ == "__main__":
    test_pocketree()