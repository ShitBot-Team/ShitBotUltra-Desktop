// The Desktop release includes this small executable as sbu.exe. It hosts the
// Core library and accepts the existing Desktop default launch arguments.
package main

import (
	"context"
	"flag"
	"fmt"
	"os"
	"os/signal"

	core "github.com/ShitBot-Team/ShitBotUltra/runtime"
)

// Supplied by the release script from the checked-out Core revision.
var version = "local"

func main() {
	if err := run(os.Args[1:]); err != nil {
		fmt.Fprintln(os.Stderr, "Core host:", err)
		os.Exit(1)
	}
}

func run(args []string) error {
	if len(args) == 0 || args[0] != "serve" {
		return fmt.Errorf("usage: sbu.exe serve [--no-webui]")
	}
	flags := flag.NewFlagSet("serve", flag.ContinueOnError)
	noWebUI := flags.Bool("no-webui", false, "disable WebUI for this Core session")
	if err := flags.Parse(args[1:]); err != nil {
		return err
	}
	if flags.NArg() != 0 {
		return fmt.Errorf("unexpected argument %q", flags.Arg(0))
	}

	options := core.Options{Version: version}
	if *noWebUI {
		enabled := false
		options.WebUI = &enabled
	}
	instance, err := core.New(options)
	if err != nil {
		return err
	}
	ctx, cancel := signal.NotifyContext(context.Background(), os.Interrupt)
	defer cancel()
	// Run releases the Core lock and closes its services when the context is
	// cancelled. Desktop sends Ctrl+C when it owns and stops this process.
	return instance.Run(ctx)
}
