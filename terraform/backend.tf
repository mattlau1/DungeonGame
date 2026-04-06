terraform {
  backend "gcs" {
    bucket = "dungeon-game-prod-terraform-state"
    prefix = "terraform/state"
  }
}
