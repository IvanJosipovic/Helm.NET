//go:build linux

package main

import "testing"

func TestActivationSignalDoesNotUseAltstack(t *testing.T) {
	switch got := activationSignalUsesAltstack(); got {
	case 0:
	case 1:
		t.Fatal("SIGRTMIN still has SA_ONSTACK; the host activation handler would run on the alternate signal stack")
	default:
		t.Fatal("could not query the SIGRTMIN disposition")
	}
}
