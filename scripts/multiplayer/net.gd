extends Node
## Session setup. Single player runs on an offline peer that is its own
## server, so the same server-authoritative code path is used everywhere.
##
## Command line (after `--`):  --host [--port=7777]   |   --join=1.2.3.4[:7777]
##                             --world=<name>  (save folder, default "first_stone")
##                             --seed=<number or words>  (only used when the world is new)

signal session_started(is_server: bool)
signal session_failed(reason: String)

const DEFAULT_PORT := 7777
const MAX_CLIENTS := 7

var world_name := "first_stone"
## Seed for a world that doesn't exist yet; -1 picks a random one. Saved worlds keep theirs.
var new_world_seed := -1
var new_world_seed_text := ""


## Any text works as a seed: numbers are used as they are, words are hashed.
static func seed_from_text(text: String) -> int:
	text = text.strip_edges()
	if text == "":
		return -1
	if text.is_valid_int():
		return absi(text.to_int()) & 0x7FFFFFFF
	return text.hash() & 0x7FFFFFFF


func leave_session() -> void:
	if multiplayer.multiplayer_peer:
		multiplayer.multiplayer_peer.close()
	multiplayer.multiplayer_peer = null


func start_single_player() -> void:
	multiplayer.multiplayer_peer = OfflineMultiplayerPeer.new()
	session_started.emit(true)


func host(port: int = DEFAULT_PORT) -> void:
	var peer := ENetMultiplayerPeer.new()
	var err := peer.create_server(port, MAX_CLIENTS)
	if err != OK:
		session_failed.emit("Could not host on port %d (error %d)" % [port, err])
		return
	multiplayer.multiplayer_peer = peer
	session_started.emit(true)


func join(address: String, port: int = DEFAULT_PORT) -> void:
	var peer := ENetMultiplayerPeer.new()
	var err := peer.create_client(address, port)
	if err != OK:
		session_failed.emit("Could not reach %s:%d (error %d)" % [address, port, err])
		return
	multiplayer.multiplayer_peer = peer
	multiplayer.connected_to_server.connect(func(): session_started.emit(false), CONNECT_ONE_SHOT)
	multiplayer.connection_failed.connect(func(): session_failed.emit("Connection failed"), CONNECT_ONE_SHOT)


## Returns {"mode": "single"|"host"|"join", "address", "port"} from the command line.
func parse_args() -> Dictionary:
	var out := {"mode": "", "address": "127.0.0.1", "port": DEFAULT_PORT}
	for arg in OS.get_cmdline_user_args():
		if arg == "--host":
			out.mode = "host"
		elif arg == "--single":
			out.mode = "single"
		elif arg.begins_with("--join="):
			out.mode = "join"
			var target := arg.trim_prefix("--join=")
			if target.contains(":"):
				out.address = target.get_slice(":", 0)
				out.port = int(target.get_slice(":", 1))
			else:
				out.address = target
		elif arg.begins_with("--port="):
			out.port = int(arg.trim_prefix("--port="))
		elif arg.begins_with("--seed="):
			new_world_seed_text = arg.trim_prefix("--seed=")
			new_world_seed = seed_from_text(new_world_seed_text)
		elif arg.begins_with("--world="):
			world_name = arg.trim_prefix("--world=").validate_filename()
	return out
