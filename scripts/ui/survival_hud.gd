extends CanvasLayer
## The survival HUD: bars, conditions, clock, hotbar, prompts, and the bag and
## crafting screen (Tab). It only shows what the sim last told this peer and
## sends requests back; it never changes game state itself.

const SLOT := 52
const GAP := 4
const BAR_W := 220.0

const CONTROLS := """CONTROLS

Move: WASD    Jump: Space    Sprint: Shift    Dodge: Ctrl
View: V    Free the mouse: Esc

Left click: dig, or attack when a weapon is held or a creature is ahead
    tap for a light blow, hold for a heavy one
Right click: place a block, eat, plant, set down. With a weapon or bare hands: guard
Middle click: lock on to a creature

1-8 or mouse wheel: choose a hotbar slot
E: pick up, harvest, drink at water, read an inscription, help a friend up
Q: speak the chosen word    R: next word    B: blast (test tool)
Tab: bag and crafting    F1: this page

Testing: F9 starter kit    F10 switch day and night    F11 call a nightstalker"""

var actions                      # player_actions.gd
var sim

var _root: Control
var _hp: ColorRect
var _stamina: ColorRect
var _debt: ColorRect
var _debt_row: Control
var _status: Label
var _clock: Label
var _held: Label
var _word: Label
var _prompt: Label
var _flash: ColorRect
var _down: Label
var _help: PanelContainer
var _bag: PanelContainer
var _hotbar: Array = []          # slot panels, 8
var _bag_slots: Array = []       # slot panels, 32
var _recipes: VBoxContainer
var _bag_note: Label
var _picked := -1                # bag slot picked up for a move, -1 if none
var _flash_left := 0.0
var _flash_color := Color.RED
var _recipe_timer := 0.0
var _icons := {}


func _ready() -> void:
	layer = 5
	_root = Control.new()
	_root.set_anchors_preset(Control.PRESET_FULL_RECT)
	_root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_root)

	_flash = ColorRect.new()
	_flash.set_anchors_preset(Control.PRESET_FULL_RECT)
	_flash.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_flash.color = Color(1, 0, 0, 0)
	_root.add_child(_flash)

	# Top left: bars, then conditions and buffs.
	var left := VBoxContainer.new()
	left.position = Vector2(16, 14)
	left.mouse_filter = Control.MOUSE_FILTER_IGNORE
	left.add_theme_constant_override("separation", 4)
	_root.add_child(left)
	_hp = _bar(left, "#c8413a", 14)
	_stamina = _bar(left, "#d9b648", 8)
	_debt = _bar(left, "#8a6fd0", 8)
	_debt_row = _debt.get_parent()
	_status = _label(left, 14)

	_clock = _label(_root, 16)
	_clock.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	_clock.grow_horizontal = Control.GROW_DIRECTION_BEGIN
	_clock.position = Vector2(-16, 12)
	_clock.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT

	# Bottom centre: prompt, held item, hotbar.
	var bottom := VBoxContainer.new()
	bottom.set_anchors_and_offsets_preset(Control.PRESET_CENTER_BOTTOM)
	bottom.grow_horizontal = Control.GROW_DIRECTION_BOTH
	bottom.grow_vertical = Control.GROW_DIRECTION_BEGIN
	bottom.position.y = -14
	bottom.alignment = BoxContainer.ALIGNMENT_END
	bottom.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_root.add_child(bottom)
	_prompt = _label(bottom, 18)
	_prompt.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_held = _label(bottom, 14)
	_held.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var bar := HBoxContainer.new()
	bar.alignment = BoxContainer.ALIGNMENT_CENTER
	bar.add_theme_constant_override("separation", GAP)
	bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	bottom.add_child(bar)
	for i in Inventory.HOTBAR:
		_hotbar.append(_slot(bar, i, false))

	_word = _label(_root, 14)
	_word.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_RIGHT)
	_word.grow_horizontal = Control.GROW_DIRECTION_BEGIN
	_word.grow_vertical = Control.GROW_DIRECTION_BEGIN
	_word.position = Vector2(-16, -16)
	_word.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT

	_down = _label(_root, 26)
	_down.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	_down.grow_horizontal = Control.GROW_DIRECTION_BOTH
	_down.position.y = -90
	_down.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

	_build_bag()
	_build_help()
	sim.inventory_changed.connect(refresh)
	sim.vitals_changed.connect(_show_vitals)
	refresh()
	_show_vitals()


# --- Building blocks ---------------------------------------------------------

func _label(parent: Node, size: int) -> Label:
	var l := Label.new()
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.9))
	l.add_theme_constant_override("shadow_offset_x", 1)
	l.add_theme_constant_override("shadow_offset_y", 1)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(l)
	return l


func _bar(parent: Node, color: String, height: int) -> ColorRect:
	var back := ColorRect.new()
	back.color = Color(0, 0, 0, 0.55)
	back.custom_minimum_size = Vector2(BAR_W + 4, height + 4)
	back.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(back)
	var fill := ColorRect.new()
	fill.color = Color(color)
	fill.position = Vector2(2, 2)
	fill.size = Vector2(BAR_W, height)
	fill.mouse_filter = Control.MOUSE_FILTER_IGNORE
	back.add_child(fill)
	return fill


func _panel_style(color: Color, border: Color, width := 2) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = color
	s.border_color = border
	s.set_border_width_all(width)
	s.set_corner_radius_all(4)
	s.set_content_margin_all(12)
	return s


## One inventory slot: a swatch (or icon), a short name and a count.
func _slot(parent: Node, index: int, clickable: bool) -> Panel:
	var p := Panel.new()
	p.custom_minimum_size = Vector2(SLOT, SLOT)
	p.mouse_filter = Control.MOUSE_FILTER_STOP if clickable else Control.MOUSE_FILTER_IGNORE
	var swatch := ColorRect.new()
	swatch.name = "Swatch"
	swatch.position = Vector2(8, 6)
	swatch.size = Vector2(SLOT - 16, SLOT - 24)
	swatch.mouse_filter = Control.MOUSE_FILTER_IGNORE
	p.add_child(swatch)
	var icon := TextureRect.new()
	icon.name = "Icon"
	icon.position = Vector2(6, 4)
	icon.size = Vector2(SLOT - 12, SLOT - 12)
	icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	icon.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
	p.add_child(icon)
	var tag := _label(p, 10)
	tag.name = "Tag"
	tag.position = Vector2(3, SLOT - 17)
	tag.size = Vector2(SLOT - 6, 14)
	tag.clip_text = true
	var count := _label(p, 12)
	count.name = "Count"
	count.position = Vector2(0, 1)
	count.size = Vector2(SLOT - 4, 14)
	count.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	var wear := ColorRect.new()
	wear.name = "Wear"
	wear.color = Color("#7fd46a")
	wear.position = Vector2(4, SLOT - 5)
	wear.size = Vector2(SLOT - 8, 3)
	wear.mouse_filter = Control.MOUSE_FILTER_IGNORE
	p.add_child(wear)
	if clickable:
		p.gui_input.connect(_on_slot_input.bind(index))
	parent.add_child(p)
	return p


func _icon(key: String) -> Texture2D:
	if not _icons.has(key):
		var art := "seeds" if GameData.item(key).get("kind", "") == "seed" else key
		_icons[key] = ModelLibrary.icon(art)
	return _icons[key]


func _fill_slot(p: Panel, index: int, chosen: bool) -> void:
	var s = sim.view_inv.slots[index]
	var border := Color("#f2e6c8") if chosen else Color(1, 1, 1, 0.18)
	if index == _picked:
		border = Color("#6fd0c0")
	p.add_theme_stylebox_override("panel", _panel_style(Color(0.08, 0.07, 0.07, 0.72), border, 2 if chosen or index == _picked else 1))
	var swatch: ColorRect = p.get_node("Swatch")
	var icon: TextureRect = p.get_node("Icon")
	var tag: Label = p.get_node("Tag")
	var count: Label = p.get_node("Count")
	var wear: ColorRect = p.get_node("Wear")
	if s == null:
		swatch.visible = false
		icon.visible = false
		wear.visible = false
		tag.text = ""
		count.text = ""
		p.tooltip_text = ""
		return
	var def := GameData.item(s.k)
	var tex := _icon(s.k)
	icon.texture = tex
	icon.visible = tex != null
	swatch.visible = tex == null
	swatch.color = Color(def.get("color", "#cccccc"))
	tag.text = "" if tex else str(def.get("name", s.k))
	count.text = str(int(s.n)) if int(s.n) > 1 else ""
	wear.visible = s.has("d") and def.has("durability")
	if wear.visible:
		var frac := clampf(float(s.d) / float(def.durability), 0.0, 1.0)
		wear.size.x = (SLOT - 8) * frac
		wear.color = Color("#7fd46a") if frac > 0.3 else Color("#d8613a")
	p.tooltip_text = _describe(def)


func _describe(def: Dictionary) -> String:
	var lines: Array = [def.get("name", "")]
	match def.get("kind", ""):
		"weapon", "tool":
			lines.append("Damage %d, reach %.1f m" % [int(def.get("damage", 0)), float(def.get("reach_m", 0))])
			if def.has("dig_power"):
				lines.append("Digs up to hardness %d" % int(def.dig_power))
		"food":
			lines.append("Right click to %s" % def.get("use", {}).get("verb", "use"))
		"seed":
			lines.append("Right click on soil to plant")
		"structure":
			lines.append("Right click on the ground to set down")
		"block":
			lines.append("Right click to place")
	return "\n".join(lines)


# --- Bag and crafting --------------------------------------------------------

func _build_bag() -> void:
	_bag = PanelContainer.new()
	_bag.add_theme_stylebox_override("panel", _panel_style(Color(0.09, 0.07, 0.07, 0.93), Color("#8a6a52")))
	_bag.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	_bag.grow_horizontal = Control.GROW_DIRECTION_BOTH
	_bag.grow_vertical = Control.GROW_DIRECTION_BOTH
	_bag.visible = false
	_root.add_child(_bag)
	var cols := HBoxContainer.new()
	cols.add_theme_constant_override("separation", 20)
	_bag.add_child(cols)

	var left := VBoxContainer.new()
	left.add_theme_constant_override("separation", 8)
	cols.add_child(left)
	_label(left, 18).text = "Bag"
	var grid := GridContainer.new()
	grid.columns = Inventory.HOTBAR
	grid.add_theme_constant_override("h_separation", GAP)
	grid.add_theme_constant_override("v_separation", GAP)
	left.add_child(grid)
	for i in Inventory.SIZE:
		_bag_slots.append(_slot(grid, i, true))
	_bag_note = _label(left, 13)
	_bag_note.text = "Top row is the hotbar. Click a slot, then another, to move or swap.\nRight click drops one, Shift + right click drops the stack."
	_bag_note.modulate = Color(1, 1, 1, 0.75)

	var right := VBoxContainer.new()
	right.add_theme_constant_override("separation", 8)
	right.custom_minimum_size = Vector2(380, 0)
	cols.add_child(right)
	_label(right, 18).text = "Make"
	var scroll := ScrollContainer.new()
	scroll.custom_minimum_size = Vector2(380, SLOT * 4 + GAP * 3 + 40)
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	right.add_child(scroll)
	_recipes = VBoxContainer.new()
	_recipes.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_recipes.add_theme_constant_override("separation", 4)
	scroll.add_child(_recipes)


func _build_help() -> void:
	_help = PanelContainer.new()
	_help.add_theme_stylebox_override("panel", _panel_style(Color(0.09, 0.07, 0.07, 0.93), Color("#8a6a52")))
	_help.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	_help.grow_horizontal = Control.GROW_DIRECTION_BOTH
	_help.grow_vertical = Control.GROW_DIRECTION_BOTH
	_help.visible = false
	_help.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_root.add_child(_help)
	_label(_help, 15).text = CONTROLS


func _on_slot_input(event: InputEvent, index: int) -> void:
	if not (event is InputEventMouseButton and event.pressed):
		return
	if event.button_index == MOUSE_BUTTON_LEFT:
		if _picked < 0:
			if sim.view_inv.slots[index] != null:
				_picked = index
		else:
			if _picked != index:
				sim.request_move_slot.rpc_id(1, _picked, index)
			_picked = -1
		refresh()
	elif event.button_index == MOUSE_BUTTON_RIGHT and sim.view_inv.slots[index] != null:
		sim.request_drop.rpc_id(1, index, event.shift_pressed)
		_picked = -1


func _fill_recipes() -> void:
	for ch in _recipes.get_children():
		ch.queue_free()
	var stations: Array = sim._stations(multiplayer.get_unique_id())
	var knows: bool = not actions.player.known_words.is_empty()
	var ready: Array = []
	var later: Array = []
	for id in GameData.recipe_order:
		var r: Dictionary = GameData.recipes[id]
		var why := Crafting.check(r, sim.view_inv, stations, knows)
		(ready if why == Crafting.OK else later).append([r, why])
	for pair in ready + later:
		var r: Dictionary = pair[0]
		var why: String = pair[1]
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 8)
		_recipes.add_child(row)
		var b := Button.new()
		b.text = "Make"
		b.disabled = why != Crafting.OK
		b.focus_mode = Control.FOCUS_NONE
		b.pressed.connect(func(): sim.request_craft.rpc_id(1, r.id))
		row.add_child(b)
		var text := _label(row, 13)
		text.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		text.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		var parts: Array = []
		for key in r["in"]:
			parts.append("%d %s" % [int(r["in"][key]), GameData.item(key).get("name", key).to_lower()])
		var n := int(r.get("n", 1))
		var title: String = GameData.item(r.out).get("name", r.out) + (" x%d" % n if n > 1 else "")
		text.text = "%s\n  %s%s" % [title, ", ".join(parts), "" if why == Crafting.OK else "  (%s)" % why]
		text.modulate = Color.WHITE if why == Crafting.OK else Color(1, 1, 1, 0.5)


# --- Called by player_actions.gd ---------------------------------------------

func toggle_help() -> void:
	_help.visible = not _help.visible


func show_bag(open: bool) -> void:
	_bag.visible = open
	_picked = -1
	if open:
		_help.visible = false
		refresh()


func flash(kind: String) -> void:
	match kind:
		"hurt", "guard_break", "downed", "die":
			_flash_color = Color(0.8, 0.05, 0.05)
			_flash_left = 0.35
		"blocked", "evade":
			_flash_color = Color(1, 1, 1)
			_flash_left = 0.12
		"respawn":
			_flash_color = Color(0.25, 0.75, 0.68)
			_flash_left = 0.6


func refresh() -> void:
	if _hotbar.is_empty():
		return
	for i in Inventory.HOTBAR:
		_fill_slot(_hotbar[i], i, i == actions.selected)
	var def: Dictionary = actions.held()
	_held.text = def.get("name", "Bare hands")
	if _bag.visible:
		for i in Inventory.SIZE:
			_fill_slot(_bag_slots[i], i, i == actions.selected)
		_fill_recipes()


# --- Per frame ---------------------------------------------------------------

func _show_vitals() -> void:
	var v: Dictionary = sim.view
	_hp.size.x = BAR_W * clampf(float(v.hp) / maxf(float(v.hp_max), 1.0), 0.0, 1.0)
	_stamina.size.x = BAR_W * clampf(float(v.stamina) / maxf(float(v.stamina_max), 1.0), 0.0, 1.0)
	var debt := float(v.get("cond", {}).get("gravity_debt", 0.0))
	_debt_row.visible = debt > 0.5
	_debt.size.x = BAR_W * clampf(debt / 100.0, 0.0, 1.0)
	var lines: Array = []
	for b in v.get("bands", []):
		lines.append(("+ " if b.get("good", false) else "! ") + str(b.name))
	var buffs: Dictionary = v.get("buffs", {})
	for id in buffs:
		lines.append("+ %s %ds" % [GameData.buffs.get(id, {}).get("name", id), int(buffs[id])])
	_status.text = "\n".join(lines)
	if v.get("dead", false):
		_down.text = "You fell. Waking at your %s..." % ("Tether" if v.get("tether", false) else "camp")
	elif float(v.get("downed", 0.0)) > 0.0:
		_down.text = "You are down. A friend can help you up (%ds)" % int(ceil(float(v.downed)))
	else:
		_down.text = ""


func _process(delta: float) -> void:
	if sim == null or not sim.active:
		_root.visible = false
		return
	_root.visible = true
	_flash_left = maxf(_flash_left - delta, 0.0)
	_flash.color = Color(_flash_color.r, _flash_color.g, _flash_color.b, minf(_flash_left, 0.3))
	_prompt.text = "" if actions.ui_open else actions.prompt
	var t: float = sim.time_of_day()
	var minutes := int(t * 24.0 * 60.0)
	_clock.text = "Day %d   %02d:%02d%s" % [Conditions.day_number(sim.world_time, sim.day_length), minutes / 60, minutes % 60,
		"   night" if sim.is_night() else ""]
	var w: String = actions.current_word()
	_word.text = "" if w == "" else "Word [Q]: %s   next [R]" % ContentDB.words[w].display
	if _bag.visible:
		_recipe_timer -= delta
		if _recipe_timer <= 0.0:
			_recipe_timer = 1.0   # stations change as the player moves
			_fill_recipes()
