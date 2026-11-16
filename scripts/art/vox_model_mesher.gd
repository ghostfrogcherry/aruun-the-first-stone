class_name VoxModelMesher
extends RefCounted
## Turns VoxFile models into a mesh: hidden faces are dropped and neighbouring faces of the
## same colour are merged into bigger quads ("greedy meshing"). Colours go into vertex
## colours. Glowing palette colours go to a second surface so they can have their own material.
##
##   var m := VoxModelMesher.new()
##   m.add(model, palette, glow_flags)        # once per part; offset moves a part
##   var mesh := m.build(0.25)                # 0.25 world units per model voxel
## Surface 0 = normal voxels, surface 1 = glowing ones (either may be missing; check
## surface_get_name: "solid" or "glow").

var _verts := [PackedVector3Array(), PackedVector3Array()]
var _normals := [PackedVector3Array(), PackedVector3Array()]
var _colors := [PackedColorArray(), PackedColorArray()]


## glow: PackedByteArray of 256 flags (1 = palette index glows), or empty for none.
func add(model: Dictionary, palette: PackedColorArray, glow := PackedByteArray(), offset := Vector3.ZERO) -> void:
	var size: Vector3i = model.size
	var vox: PackedByteArray = model.voxels
	var dims := [size.x, size.y, size.z]
	var stride := [1, size.x, size.x * size.y]
	for d in 3:
		var u := (d + 1) % 3
		var v := (d + 2) % 3
		var nu: int = dims[u]
		var nv: int = dims[v]
		var mask := PackedByteArray()
		mask.resize(nu * nv)
		for side in 2: # 0: face looks along +d, 1: along -d
			var step: int = stride[d] if side == 0 else -stride[d]
			var edge: int = dims[d] - 1 if side == 0 else 0
			for slice in dims[d]:
				var any := false
				for j in nv:
					for i in nu:
						var idx: int = slice * stride[d] + i * stride[u] + j * stride[v]
						var c := vox[idx]
						if c != 0 and slice != edge and vox[idx + step] != 0:
							c = 0 # covered by a neighbour
						mask[i + j * nu] = c
						if c != 0:
							any = true
				if any:
					_emit_slice(mask, nu, nv, d, u, v, slice + (1 if side == 0 else 0), side, palette, glow, offset)


func build(scale := 1.0) -> ArrayMesh:
	var mesh := ArrayMesh.new()
	for s in 2:
		if _verts[s].is_empty():
			continue
		var verts: PackedVector3Array = _verts[s]
		if scale != 1.0:
			for i in verts.size():
				verts[i] *= scale
		var arrays := []
		arrays.resize(Mesh.ARRAY_MAX)
		arrays[Mesh.ARRAY_VERTEX] = verts
		arrays[Mesh.ARRAY_NORMAL] = _normals[s]
		arrays[Mesh.ARRAY_COLOR] = _colors[s]
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
		mesh.surface_set_name(mesh.get_surface_count() - 1, "solid" if s == 0 else "glow")
	return mesh


func _emit_slice(mask: PackedByteArray, nu: int, nv: int, d: int, u: int, v: int, plane: int, side: int,
		palette: PackedColorArray, glow: PackedByteArray, offset: Vector3) -> void:
	var normal := Vector3.ZERO
	normal[d] = 1.0 if side == 0 else -1.0
	for j in nv:
		var i := 0
		while i < nu:
			var c := mask[i + j * nu]
			if c == 0:
				i += 1
				continue
			var w := 1
			while i + w < nu and mask[i + w + j * nu] == c:
				w += 1
			var h := 1
			var grow := true
			while j + h < nv and grow:
				for k in w:
					if mask[i + k + (j + h) * nu] != c:
						grow = false
						break
				if grow:
					h += 1
			for b in h:
				for a in w:
					mask[i + a + (j + b) * nu] = 0

			var p0 := Vector3.ZERO
			p0[d] = plane
			p0[u] = i
			p0[v] = j
			p0 += offset
			var du := Vector3.ZERO
			du[u] = w
			var dv := Vector3.ZERO
			dv[v] = h
			var s := 1 if (not glow.is_empty() and glow[c] != 0) else 0
			var quad: Array = [p0, p0 + du + dv, p0 + du, p0, p0 + dv, p0 + du + dv] if side == 0 \
					else [p0, p0 + du, p0 + du + dv, p0, p0 + du + dv, p0 + dv]
			for p in quad: # clockwise seen from outside, as Godot expects
				_verts[s].append(p)
				_normals[s].append(normal)
				_colors[s].append(palette[c])
			i += w
