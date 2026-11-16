class_name Inventory
extends RefCounted
## A character's bag: fixed slots, the first HOTBAR of them are the hotbar.
## Pure logic. The server owns the real one; clients hold a copy for display.
## A slot is null or {"k": item key, "n": count, "d": durability left (tools only)}.

const SIZE := 32
const HOTBAR := 8

var slots: Array = []


func _init() -> void:
	slots.resize(SIZE)


## Adds items, stacking first. Returns how many did not fit.
func add(key: String, n: int, durability := -1) -> int:
	var def := GameData.item(key)
	if def.is_empty() or n <= 0:
		return n
	var stack := int(def.get("stack", 1))
	if stack > 1:
		for s in slots:
			if s != null and s.k == key and s.n < stack:
				var put := mini(n, stack - s.n)
				s.n += put
				n -= put
				if n == 0:
					return 0
	for i in SIZE:
		if slots[i] == null:
			var put := mini(n, stack)
			var slot := {"k": key, "n": put}
			if def.has("durability"):
				slot["d"] = durability if durability >= 0 else int(def.durability)
			slots[i] = slot
			n -= put
			if n == 0:
				return 0
	return n


func count(key: String) -> int:
	var total := 0
	for s in slots:
		if s != null and s.k == key:
			total += s.n
	return total


func has_all(needs: Dictionary) -> bool:
	for key in needs:
		if count(key) < int(needs[key]):
			return false
	return true


## Removes exactly n of key, or nothing. Returns true if removed.
func remove(key: String, n: int) -> bool:
	if count(key) < n:
		return false
	for i in range(SIZE - 1, -1, -1): # empty the back of the bag before the hotbar
		var s = slots[i]
		if s != null and s.k == key:
			var take := mini(n, s.n)
			s.n -= take
			n -= take
			if s.n == 0:
				slots[i] = null
			if n == 0:
				break
	return true


## Takes up to n from one slot. Returns the taken stack ({} if none).
func take_slot(i: int, n: int) -> Dictionary:
	if i < 0 or i >= SIZE or slots[i] == null or n <= 0:
		return {}
	var s: Dictionary = slots[i]
	var take := mini(n, s.n)
	var out: Dictionary = s.duplicate()
	out.n = take
	s.n -= take
	if s.n == 0:
		slots[i] = null
	return out


func key_at(i: int) -> String:
	if i < 0 or i >= SIZE or slots[i] == null:
		return ""
	return slots[i].k


## Moves slot a onto slot b: merges equal stacks, otherwise swaps.
func move(a: int, b: int) -> bool:
	if a == b or a < 0 or b < 0 or a >= SIZE or b >= SIZE or slots[a] == null:
		return false
	var from: Dictionary = slots[a]
	var to = slots[b]
	if to != null and to.k == from.k and not from.has("d"):
		var stack := int(GameData.item(from.k).get("stack", 1))
		var put := mini(from.n, stack - to.n)
		to.n += put
		from.n -= put
		if from.n == 0:
			slots[a] = null
		return put > 0
	slots[a] = to
	slots[b] = from
	return true


## Wears a tool by one use. Returns true if it broke (and was removed).
func wear(i: int, amount := 1) -> bool:
	var s = slots[i] if i >= 0 and i < SIZE else null
	if s == null or not s.has("d"):
		return false
	s.d -= amount
	if s.d <= 0:
		slots[i] = null
		return true
	return false


func is_empty() -> bool:
	for s in slots:
		if s != null:
			return false
	return true


func clear() -> Array:
	var out: Array = []
	for i in SIZE:
		if slots[i] != null:
			out.append(slots[i])
			slots[i] = null
	return out


func to_data() -> Array:
	return slots.duplicate(true)


func from_data(data: Array) -> void:
	slots = []
	slots.resize(SIZE)
	for i in mini(data.size(), SIZE):
		var s = data[i]
		if typeof(s) == TYPE_DICTIONARY and GameData.items.has(s.get("k", "")):
			var slot := {"k": String(s.k), "n": int(s.n)}
			if s.has("d"):
				slot["d"] = int(s.d)
			slots[i] = slot
