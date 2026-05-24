#!/bin/bash
# Convenience script to run the pygame client

cd "$(dirname "$0")"

# Activate virtual environment
source venv/bin/activate

# Run the game with any passed arguments
python main.py "$@"
