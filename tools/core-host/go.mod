module github.com/ShitBot-Team/ShitBotUltra-Desktop/core-host

go 1.24.0

// The release script supplies the matching Core source through a temporary
// Go workspace. The host contains no Core business implementation of its own.
require github.com/ShitBot-Team/ShitBotUltra v0.0.0
