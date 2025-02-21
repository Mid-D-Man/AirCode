/*
 * ATTENTION: The "eval" devtool has been used (maybe by default in mode: "development").
 * This devtool is neither made for production nor for readable output files.
 * It uses "eval()" calls to create a separate source file in the browser devtools.
 * If you are trying to read the output file, select a different devtool (https://webpack.js.org/configuration/devtool/)
 * or disable the default devtool with "devtool: false".
 * If you are looking for production-ready output files, see mode: "production" (https://webpack.js.org/configuration/mode/).
 */
(function webpackUniversalModuleDefinition(root, factory) {
	if(typeof exports === 'object' && typeof module === 'object')
		module.exports = factory();
	else if(typeof define === 'function' && define.amd)
		define([], factory);
	else if(typeof exports === 'object')
		exports["midAirCodeLib"] = factory();
	else
		root["midAirCodeLib"] = factory();
})(self, () => {
return /******/ (() => { // webpackBootstrap
/******/ 	"use strict";
/******/ 	var __webpack_modules__ = ({

/***/ "./js/index.js":
/*!*********************!*\
  !*** ./js/index.js ***!
  \*********************/
/***/ ((module, __webpack_exports__, __webpack_require__) => {

eval("__webpack_require__.a(module, async (__webpack_handle_async_dependencies__, __webpack_async_result__) => { try {\n__webpack_require__.r(__webpack_exports__);\n/* harmony export */ __webpack_require__.d(__webpack_exports__, {\n/* harmony export */   defaultQrOptions: () => (/* binding */ defaultQrOptions),\n/* harmony export */   initialize: () => (/* binding */ initialize)\n/* harmony export */ });\n/* harmony import */ var _pkg_mid_air_code_lib__WEBPACK_IMPORTED_MODULE_0__ = __webpack_require__(/*! ../pkg/mid_air_code_lib */ \"./pkg/mid_air_code_lib.js\");\n/* harmony import */ var _pkg_mid_air_code_lib__WEBPACK_IMPORTED_MODULE_1__ = __webpack_require__(/*! ../pkg/mid_air_code_lib */ \"./pkg/mid_air_code_lib_bg.js\");\nvar __webpack_async_dependencies__ = __webpack_handle_async_dependencies__([_pkg_mid_air_code_lib__WEBPACK_IMPORTED_MODULE_0__]);\n_pkg_mid_air_code_lib__WEBPACK_IMPORTED_MODULE_0__ = (__webpack_async_dependencies__.then ? (await __webpack_async_dependencies__)() : __webpack_async_dependencies__)[0];\nfunction _regeneratorRuntime() { \"use strict\"; /*! regenerator-runtime -- Copyright (c) 2014-present, Facebook, Inc. -- license (MIT): https://github.com/facebook/regenerator/blob/main/LICENSE */ _regeneratorRuntime = function _regeneratorRuntime() { return e; }; var t, e = {}, r = Object.prototype, n = r.hasOwnProperty, o = Object.defineProperty || function (t, e, r) { t[e] = r.value; }, i = \"function\" == typeof Symbol ? Symbol : {}, a = i.iterator || \"@@iterator\", c = i.asyncIterator || \"@@asyncIterator\", u = i.toStringTag || \"@@toStringTag\"; function define(t, e, r) { return Object.defineProperty(t, e, { value: r, enumerable: !0, configurable: !0, writable: !0 }), t[e]; } try { define({}, \"\"); } catch (t) { define = function define(t, e, r) { return t[e] = r; }; } function wrap(t, e, r, n) { var i = e && e.prototype instanceof Generator ? e : Generator, a = Object.create(i.prototype), c = new Context(n || []); return o(a, \"_invoke\", { value: makeInvokeMethod(t, r, c) }), a; } function tryCatch(t, e, r) { try { return { type: \"normal\", arg: t.call(e, r) }; } catch (t) { return { type: \"throw\", arg: t }; } } e.wrap = wrap; var h = \"suspendedStart\", l = \"suspendedYield\", f = \"executing\", s = \"completed\", y = {}; function Generator() {} function GeneratorFunction() {} function GeneratorFunctionPrototype() {} var p = {}; define(p, a, function () { return this; }); var d = Object.getPrototypeOf, v = d && d(d(values([]))); v && v !== r && n.call(v, a) && (p = v); var g = GeneratorFunctionPrototype.prototype = Generator.prototype = Object.create(p); function defineIteratorMethods(t) { [\"next\", \"throw\", \"return\"].forEach(function (e) { define(t, e, function (t) { return this._invoke(e, t); }); }); } function AsyncIterator(t, e) { function invoke(r, o, i, a) { var c = tryCatch(t[r], t, o); if (\"throw\" !== c.type) { var u = c.arg, h = u.value; return h && \"object\" == _typeof(h) && n.call(h, \"__await\") ? e.resolve(h.__await).then(function (t) { invoke(\"next\", t, i, a); }, function (t) { invoke(\"throw\", t, i, a); }) : e.resolve(h).then(function (t) { u.value = t, i(u); }, function (t) { return invoke(\"throw\", t, i, a); }); } a(c.arg); } var r; o(this, \"_invoke\", { value: function value(t, n) { function callInvokeWithMethodAndArg() { return new e(function (e, r) { invoke(t, n, e, r); }); } return r = r ? r.then(callInvokeWithMethodAndArg, callInvokeWithMethodAndArg) : callInvokeWithMethodAndArg(); } }); } function makeInvokeMethod(e, r, n) { var o = h; return function (i, a) { if (o === f) throw Error(\"Generator is already running\"); if (o === s) { if (\"throw\" === i) throw a; return { value: t, done: !0 }; } for (n.method = i, n.arg = a;;) { var c = n.delegate; if (c) { var u = maybeInvokeDelegate(c, n); if (u) { if (u === y) continue; return u; } } if (\"next\" === n.method) n.sent = n._sent = n.arg;else if (\"throw\" === n.method) { if (o === h) throw o = s, n.arg; n.dispatchException(n.arg); } else \"return\" === n.method && n.abrupt(\"return\", n.arg); o = f; var p = tryCatch(e, r, n); if (\"normal\" === p.type) { if (o = n.done ? s : l, p.arg === y) continue; return { value: p.arg, done: n.done }; } \"throw\" === p.type && (o = s, n.method = \"throw\", n.arg = p.arg); } }; } function maybeInvokeDelegate(e, r) { var n = r.method, o = e.iterator[n]; if (o === t) return r.delegate = null, \"throw\" === n && e.iterator[\"return\"] && (r.method = \"return\", r.arg = t, maybeInvokeDelegate(e, r), \"throw\" === r.method) || \"return\" !== n && (r.method = \"throw\", r.arg = new TypeError(\"The iterator does not provide a '\" + n + \"' method\")), y; var i = tryCatch(o, e.iterator, r.arg); if (\"throw\" === i.type) return r.method = \"throw\", r.arg = i.arg, r.delegate = null, y; var a = i.arg; return a ? a.done ? (r[e.resultName] = a.value, r.next = e.nextLoc, \"return\" !== r.method && (r.method = \"next\", r.arg = t), r.delegate = null, y) : a : (r.method = \"throw\", r.arg = new TypeError(\"iterator result is not an object\"), r.delegate = null, y); } function pushTryEntry(t) { var e = { tryLoc: t[0] }; 1 in t && (e.catchLoc = t[1]), 2 in t && (e.finallyLoc = t[2], e.afterLoc = t[3]), this.tryEntries.push(e); } function resetTryEntry(t) { var e = t.completion || {}; e.type = \"normal\", delete e.arg, t.completion = e; } function Context(t) { this.tryEntries = [{ tryLoc: \"root\" }], t.forEach(pushTryEntry, this), this.reset(!0); } function values(e) { if (e || \"\" === e) { var r = e[a]; if (r) return r.call(e); if (\"function\" == typeof e.next) return e; if (!isNaN(e.length)) { var o = -1, i = function next() { for (; ++o < e.length;) if (n.call(e, o)) return next.value = e[o], next.done = !1, next; return next.value = t, next.done = !0, next; }; return i.next = i; } } throw new TypeError(_typeof(e) + \" is not iterable\"); } return GeneratorFunction.prototype = GeneratorFunctionPrototype, o(g, \"constructor\", { value: GeneratorFunctionPrototype, configurable: !0 }), o(GeneratorFunctionPrototype, \"constructor\", { value: GeneratorFunction, configurable: !0 }), GeneratorFunction.displayName = define(GeneratorFunctionPrototype, u, \"GeneratorFunction\"), e.isGeneratorFunction = function (t) { var e = \"function\" == typeof t && t.constructor; return !!e && (e === GeneratorFunction || \"GeneratorFunction\" === (e.displayName || e.name)); }, e.mark = function (t) { return Object.setPrototypeOf ? Object.setPrototypeOf(t, GeneratorFunctionPrototype) : (t.__proto__ = GeneratorFunctionPrototype, define(t, u, \"GeneratorFunction\")), t.prototype = Object.create(g), t; }, e.awrap = function (t) { return { __await: t }; }, defineIteratorMethods(AsyncIterator.prototype), define(AsyncIterator.prototype, c, function () { return this; }), e.AsyncIterator = AsyncIterator, e.async = function (t, r, n, o, i) { void 0 === i && (i = Promise); var a = new AsyncIterator(wrap(t, r, n, o), i); return e.isGeneratorFunction(r) ? a : a.next().then(function (t) { return t.done ? t.value : a.next(); }); }, defineIteratorMethods(g), define(g, u, \"Generator\"), define(g, a, function () { return this; }), define(g, \"toString\", function () { return \"[object Generator]\"; }), e.keys = function (t) { var e = Object(t), r = []; for (var n in e) r.push(n); return r.reverse(), function next() { for (; r.length;) { var t = r.pop(); if (t in e) return next.value = t, next.done = !1, next; } return next.done = !0, next; }; }, e.values = values, Context.prototype = { constructor: Context, reset: function reset(e) { if (this.prev = 0, this.next = 0, this.sent = this._sent = t, this.done = !1, this.delegate = null, this.method = \"next\", this.arg = t, this.tryEntries.forEach(resetTryEntry), !e) for (var r in this) \"t\" === r.charAt(0) && n.call(this, r) && !isNaN(+r.slice(1)) && (this[r] = t); }, stop: function stop() { this.done = !0; var t = this.tryEntries[0].completion; if (\"throw\" === t.type) throw t.arg; return this.rval; }, dispatchException: function dispatchException(e) { if (this.done) throw e; var r = this; function handle(n, o) { return a.type = \"throw\", a.arg = e, r.next = n, o && (r.method = \"next\", r.arg = t), !!o; } for (var o = this.tryEntries.length - 1; o >= 0; --o) { var i = this.tryEntries[o], a = i.completion; if (\"root\" === i.tryLoc) return handle(\"end\"); if (i.tryLoc <= this.prev) { var c = n.call(i, \"catchLoc\"), u = n.call(i, \"finallyLoc\"); if (c && u) { if (this.prev < i.catchLoc) return handle(i.catchLoc, !0); if (this.prev < i.finallyLoc) return handle(i.finallyLoc); } else if (c) { if (this.prev < i.catchLoc) return handle(i.catchLoc, !0); } else { if (!u) throw Error(\"try statement without catch or finally\"); if (this.prev < i.finallyLoc) return handle(i.finallyLoc); } } } }, abrupt: function abrupt(t, e) { for (var r = this.tryEntries.length - 1; r >= 0; --r) { var o = this.tryEntries[r]; if (o.tryLoc <= this.prev && n.call(o, \"finallyLoc\") && this.prev < o.finallyLoc) { var i = o; break; } } i && (\"break\" === t || \"continue\" === t) && i.tryLoc <= e && e <= i.finallyLoc && (i = null); var a = i ? i.completion : {}; return a.type = t, a.arg = e, i ? (this.method = \"next\", this.next = i.finallyLoc, y) : this.complete(a); }, complete: function complete(t, e) { if (\"throw\" === t.type) throw t.arg; return \"break\" === t.type || \"continue\" === t.type ? this.next = t.arg : \"return\" === t.type ? (this.rval = this.arg = t.arg, this.method = \"return\", this.next = \"end\") : \"normal\" === t.type && e && (this.next = e), y; }, finish: function finish(t) { for (var e = this.tryEntries.length - 1; e >= 0; --e) { var r = this.tryEntries[e]; if (r.finallyLoc === t) return this.complete(r.completion, r.afterLoc), resetTryEntry(r), y; } }, \"catch\": function _catch(t) { for (var e = this.tryEntries.length - 1; e >= 0; --e) { var r = this.tryEntries[e]; if (r.tryLoc === t) { var n = r.completion; if (\"throw\" === n.type) { var o = n.arg; resetTryEntry(r); } return o; } } throw Error(\"illegal catch attempt\"); }, delegateYield: function delegateYield(e, r, n) { return this.delegate = { iterator: values(e), resultName: r, nextLoc: n }, \"next\" === this.method && (this.arg = t), y; } }, e; }\nfunction asyncGeneratorStep(n, t, e, r, o, a, c) { try { var i = n[a](c), u = i.value; } catch (n) { return void e(n); } i.done ? t(u) : Promise.resolve(u).then(r, o); }\nfunction _asyncToGenerator(n) { return function () { var t = this, e = arguments; return new Promise(function (r, o) { var a = n.apply(t, e); function _next(n) { asyncGeneratorStep(a, r, o, _next, _throw, \"next\", n); } function _throw(n) { asyncGeneratorStep(a, r, o, _next, _throw, \"throw\", n); } _next(void 0); }); }; }\nfunction _typeof(o) { \"@babel/helpers - typeof\"; return _typeof = \"function\" == typeof Symbol && \"symbol\" == typeof Symbol.iterator ? function (o) { return typeof o; } : function (o) { return o && \"function\" == typeof Symbol && o.constructor === Symbol && o !== Symbol.prototype ? \"symbol\" : typeof o; }, _typeof(o); }\nfunction ownKeys(e, r) { var t = Object.keys(e); if (Object.getOwnPropertySymbols) { var o = Object.getOwnPropertySymbols(e); r && (o = o.filter(function (r) { return Object.getOwnPropertyDescriptor(e, r).enumerable; })), t.push.apply(t, o); } return t; }\nfunction _objectSpread(e) { for (var r = 1; r < arguments.length; r++) { var t = null != arguments[r] ? arguments[r] : {}; r % 2 ? ownKeys(Object(t), !0).forEach(function (r) { _defineProperty(e, r, t[r]); }) : Object.getOwnPropertyDescriptors ? Object.defineProperties(e, Object.getOwnPropertyDescriptors(t)) : ownKeys(Object(t)).forEach(function (r) { Object.defineProperty(e, r, Object.getOwnPropertyDescriptor(t, r)); }); } return e; }\nfunction _defineProperty(e, r, t) { return (r = _toPropertyKey(r)) in e ? Object.defineProperty(e, r, { value: t, enumerable: !0, configurable: !0, writable: !0 }) : e[r] = t, e; }\nfunction _classCallCheck(a, n) { if (!(a instanceof n)) throw new TypeError(\"Cannot call a class as a function\"); }\nfunction _defineProperties(e, r) { for (var t = 0; t < r.length; t++) { var o = r[t]; o.enumerable = o.enumerable || !1, o.configurable = !0, \"value\" in o && (o.writable = !0), Object.defineProperty(e, _toPropertyKey(o.key), o); } }\nfunction _createClass(e, r, t) { return r && _defineProperties(e.prototype, r), t && _defineProperties(e, t), Object.defineProperty(e, \"prototype\", { writable: !1 }), e; }\nfunction _toPropertyKey(t) { var i = _toPrimitive(t, \"string\"); return \"symbol\" == _typeof(i) ? i : i + \"\"; }\nfunction _toPrimitive(t, r) { if (\"object\" != _typeof(t) || !t) return t; var e = t[Symbol.toPrimitive]; if (void 0 !== e) { var i = e.call(t, r || \"default\"); if (\"object\" != _typeof(i)) return i; throw new TypeError(\"@@toPrimitive must return a primitive value.\"); } return (\"string\" === r ? String : Number)(t); }\n\n\n// Example QR code options\nvar defaultQrOptions = {\n  content: \"https://example.com\",\n  size: 256,\n  error_correction: \"High\",\n  foreground_color: {\n    r: 0,\n    g: 0,\n    b: 0,\n    a: 255\n  },\n  background_color: {\n    r: 255,\n    g: 255,\n    b: 255,\n    a: 255\n  },\n  margin: 4,\n  output_format: \"SVG\"\n};\nvar MidAirCodeWrapper = /*#__PURE__*/function () {\n  function MidAirCodeWrapper(instance) {\n    _classCallCheck(this, MidAirCodeWrapper);\n    this.instance = instance;\n  }\n  return _createClass(MidAirCodeWrapper, [{\n    key: \"generateQr\",\n    value: function generateQr() {\n      var options = arguments.length > 0 && arguments[0] !== undefined ? arguments[0] : {};\n      var mergedOptions = _objectSpread(_objectSpread({}, defaultQrOptions), options);\n      return this.instance.generate_qr(mergedOptions);\n    }\n  }, {\n    key: \"encrypt\",\n    value: function encrypt(data) {\n      return this.instance.encrypt(data);\n    }\n  }, {\n    key: \"decrypt\",\n    value: function decrypt(data) {\n      return this.instance.decrypt(data);\n    }\n  }]);\n}();\nfunction initialize() {\n  return _initialize.apply(this, arguments);\n}\nfunction _initialize() {\n  _initialize = _asyncToGenerator(/*#__PURE__*/_regeneratorRuntime().mark(function _callee() {\n    var instance;\n    return _regeneratorRuntime().wrap(function _callee$(_context) {\n      while (1) switch (_context.prev = _context.next) {\n        case 0:\n          _context.next = 2;\n          return (0,_pkg_mid_air_code_lib__WEBPACK_IMPORTED_MODULE_0__[\"default\"])();\n        case 2:\n          instance = new _pkg_mid_air_code_lib__WEBPACK_IMPORTED_MODULE_1__.MidAirCode();\n          return _context.abrupt(\"return\", new MidAirCodeWrapper(instance));\n        case 4:\n        case \"end\":\n          return _context.stop();\n      }\n    }, _callee);\n  }));\n  return _initialize.apply(this, arguments);\n}\n\n__webpack_async_result__();\n} catch(e) { __webpack_async_result__(e); } });\n\n//# sourceURL=webpack://midAirCodeLib/./js/index.js?");

/***/ }),

/***/ "./pkg/mid_air_code_lib.js":
/*!*********************************!*\
  !*** ./pkg/mid_air_code_lib.js ***!
  \*********************************/
/***/ ((__webpack_module__, __webpack_exports__, __webpack_require__) => {

eval("__webpack_require__.a(__webpack_module__, async (__webpack_handle_async_dependencies__, __webpack_async_result__) => { try {\n__webpack_require__.r(__webpack_exports__);\n/* harmony export */ __webpack_require__.d(__webpack_exports__, {\n/* harmony export */   MidAirCode: () => (/* reexport safe */ _mid_air_code_lib_bg_js__WEBPACK_IMPORTED_MODULE_0__.MidAirCode),\n/* harmony export */   __wbg_set_wasm: () => (/* reexport safe */ _mid_air_code_lib_bg_js__WEBPACK_IMPORTED_MODULE_0__.__wbg_set_wasm),\n/* harmony export */   __wbindgen_init_externref_table: () => (/* reexport safe */ _mid_air_code_lib_bg_js__WEBPACK_IMPORTED_MODULE_0__.__wbindgen_init_externref_table),\n/* harmony export */   __wbindgen_json_serialize: () => (/* reexport safe */ _mid_air_code_lib_bg_js__WEBPACK_IMPORTED_MODULE_0__.__wbindgen_json_serialize),\n/* harmony export */   __wbindgen_string_new: () => (/* reexport safe */ _mid_air_code_lib_bg_js__WEBPACK_IMPORTED_MODULE_0__.__wbindgen_string_new),\n/* harmony export */   __wbindgen_throw: () => (/* reexport safe */ _mid_air_code_lib_bg_js__WEBPACK_IMPORTED_MODULE_0__.__wbindgen_throw)\n/* harmony export */ });\n/* harmony import */ var _mid_air_code_lib_bg_wasm__WEBPACK_IMPORTED_MODULE_1__ = __webpack_require__(/*! ./mid_air_code_lib_bg.wasm */ \"./pkg/mid_air_code_lib_bg.wasm\");\n/* harmony import */ var _mid_air_code_lib_bg_js__WEBPACK_IMPORTED_MODULE_0__ = __webpack_require__(/*! ./mid_air_code_lib_bg.js */ \"./pkg/mid_air_code_lib_bg.js\");\nvar __webpack_async_dependencies__ = __webpack_handle_async_dependencies__([_mid_air_code_lib_bg_wasm__WEBPACK_IMPORTED_MODULE_1__]);\n_mid_air_code_lib_bg_wasm__WEBPACK_IMPORTED_MODULE_1__ = (__webpack_async_dependencies__.then ? (await __webpack_async_dependencies__)() : __webpack_async_dependencies__)[0];\n\n\n\n(0,_mid_air_code_lib_bg_js__WEBPACK_IMPORTED_MODULE_0__.__wbg_set_wasm)(_mid_air_code_lib_bg_wasm__WEBPACK_IMPORTED_MODULE_1__);\n_mid_air_code_lib_bg_wasm__WEBPACK_IMPORTED_MODULE_1__.__wbindgen_start();\n__webpack_async_result__();\n} catch(e) { __webpack_async_result__(e); } });\n\n//# sourceURL=webpack://midAirCodeLib/./pkg/mid_air_code_lib.js?");

/***/ }),

/***/ "./pkg/mid_air_code_lib_bg.js":
/*!************************************!*\
  !*** ./pkg/mid_air_code_lib_bg.js ***!
  \************************************/
/***/ ((__unused_webpack___webpack_module__, __webpack_exports__, __webpack_require__) => {

eval("__webpack_require__.r(__webpack_exports__);\n/* harmony export */ __webpack_require__.d(__webpack_exports__, {\n/* harmony export */   MidAirCode: () => (/* binding */ MidAirCode),\n/* harmony export */   __wbg_set_wasm: () => (/* binding */ __wbg_set_wasm),\n/* harmony export */   __wbindgen_init_externref_table: () => (/* binding */ __wbindgen_init_externref_table),\n/* harmony export */   __wbindgen_json_serialize: () => (/* binding */ __wbindgen_json_serialize),\n/* harmony export */   __wbindgen_string_new: () => (/* binding */ __wbindgen_string_new),\n/* harmony export */   __wbindgen_throw: () => (/* binding */ __wbindgen_throw)\n/* harmony export */ });\nfunction _typeof(o) { \"@babel/helpers - typeof\"; return _typeof = \"function\" == typeof Symbol && \"symbol\" == typeof Symbol.iterator ? function (o) { return typeof o; } : function (o) { return o && \"function\" == typeof Symbol && o.constructor === Symbol && o !== Symbol.prototype ? \"symbol\" : typeof o; }, _typeof(o); }\nfunction _classCallCheck(a, n) { if (!(a instanceof n)) throw new TypeError(\"Cannot call a class as a function\"); }\nfunction _defineProperties(e, r) { for (var t = 0; t < r.length; t++) { var o = r[t]; o.enumerable = o.enumerable || !1, o.configurable = !0, \"value\" in o && (o.writable = !0), Object.defineProperty(e, _toPropertyKey(o.key), o); } }\nfunction _createClass(e, r, t) { return r && _defineProperties(e.prototype, r), t && _defineProperties(e, t), Object.defineProperty(e, \"prototype\", { writable: !1 }), e; }\nfunction _toPropertyKey(t) { var i = _toPrimitive(t, \"string\"); return \"symbol\" == _typeof(i) ? i : i + \"\"; }\nfunction _toPrimitive(t, r) { if (\"object\" != _typeof(t) || !t) return t; var e = t[Symbol.toPrimitive]; if (void 0 !== e) { var i = e.call(t, r || \"default\"); if (\"object\" != _typeof(i)) return i; throw new TypeError(\"@@toPrimitive must return a primitive value.\"); } return (\"string\" === r ? String : Number)(t); }\nvar wasm;\nfunction __wbg_set_wasm(val) {\n  wasm = val;\n}\nvar WASM_VECTOR_LEN = 0;\nvar cachedUint8ArrayMemory0 = null;\nfunction getUint8ArrayMemory0() {\n  if (cachedUint8ArrayMemory0 === null || cachedUint8ArrayMemory0.byteLength === 0) {\n    cachedUint8ArrayMemory0 = new Uint8Array(wasm.memory.buffer);\n  }\n  return cachedUint8ArrayMemory0;\n}\nvar lTextEncoder = typeof TextEncoder === 'undefined' ? (0, module.require)('util').TextEncoder : TextEncoder;\nvar cachedTextEncoder = new lTextEncoder('utf-8');\nvar encodeString = typeof cachedTextEncoder.encodeInto === 'function' ? function (arg, view) {\n  return cachedTextEncoder.encodeInto(arg, view);\n} : function (arg, view) {\n  var buf = cachedTextEncoder.encode(arg);\n  view.set(buf);\n  return {\n    read: arg.length,\n    written: buf.length\n  };\n};\nfunction passStringToWasm0(arg, malloc, realloc) {\n  if (realloc === undefined) {\n    var buf = cachedTextEncoder.encode(arg);\n    var _ptr = malloc(buf.length, 1) >>> 0;\n    getUint8ArrayMemory0().subarray(_ptr, _ptr + buf.length).set(buf);\n    WASM_VECTOR_LEN = buf.length;\n    return _ptr;\n  }\n  var len = arg.length;\n  var ptr = malloc(len, 1) >>> 0;\n  var mem = getUint8ArrayMemory0();\n  var offset = 0;\n  for (; offset < len; offset++) {\n    var code = arg.charCodeAt(offset);\n    if (code > 0x7F) break;\n    mem[ptr + offset] = code;\n  }\n  if (offset !== len) {\n    if (offset !== 0) {\n      arg = arg.slice(offset);\n    }\n    ptr = realloc(ptr, len, len = offset + arg.length * 3, 1) >>> 0;\n    var view = getUint8ArrayMemory0().subarray(ptr + offset, ptr + len);\n    var ret = encodeString(arg, view);\n    offset += ret.written;\n    ptr = realloc(ptr, len, offset, 1) >>> 0;\n  }\n  WASM_VECTOR_LEN = offset;\n  return ptr;\n}\nvar cachedDataViewMemory0 = null;\nfunction getDataViewMemory0() {\n  if (cachedDataViewMemory0 === null || cachedDataViewMemory0.buffer.detached === true || cachedDataViewMemory0.buffer.detached === undefined && cachedDataViewMemory0.buffer !== wasm.memory.buffer) {\n    cachedDataViewMemory0 = new DataView(wasm.memory.buffer);\n  }\n  return cachedDataViewMemory0;\n}\nvar lTextDecoder = typeof TextDecoder === 'undefined' ? (0, module.require)('util').TextDecoder : TextDecoder;\nvar cachedTextDecoder = new lTextDecoder('utf-8', {\n  ignoreBOM: true,\n  fatal: true\n});\ncachedTextDecoder.decode();\nfunction getStringFromWasm0(ptr, len) {\n  ptr = ptr >>> 0;\n  return cachedTextDecoder.decode(getUint8ArrayMemory0().subarray(ptr, ptr + len));\n}\nfunction takeFromExternrefTable0(idx) {\n  var value = wasm.__wbindgen_export_0.get(idx);\n  wasm.__externref_table_dealloc(idx);\n  return value;\n}\nvar MidAirCodeFinalization = typeof FinalizationRegistry === 'undefined' ? {\n  register: function register() {},\n  unregister: function unregister() {}\n} : new FinalizationRegistry(function (ptr) {\n  return wasm.__wbg_midaircode_free(ptr >>> 0, 1);\n});\nvar MidAirCode = /*#__PURE__*/function () {\n  function MidAirCode() {\n    _classCallCheck(this, MidAirCode);\n    var ret = wasm.midaircode_new();\n    this.__wbg_ptr = ret >>> 0;\n    MidAirCodeFinalization.register(this, this.__wbg_ptr, this);\n    return this;\n  }\n  /**\n   * @param {any} options\n   * @returns {string}\n   */\n  return _createClass(MidAirCode, [{\n    key: \"__destroy_into_raw\",\n    value: function __destroy_into_raw() {\n      var ptr = this.__wbg_ptr;\n      this.__wbg_ptr = 0;\n      MidAirCodeFinalization.unregister(this);\n      return ptr;\n    }\n  }, {\n    key: \"free\",\n    value: function free() {\n      var ptr = this.__destroy_into_raw();\n      wasm.__wbg_midaircode_free(ptr, 0);\n    }\n  }, {\n    key: \"generate_qr\",\n    value: function generate_qr(options) {\n      var deferred2_0;\n      var deferred2_1;\n      try {\n        var ret = wasm.midaircode_generate_qr(this.__wbg_ptr, options);\n        var ptr1 = ret[0];\n        var len1 = ret[1];\n        if (ret[3]) {\n          ptr1 = 0;\n          len1 = 0;\n          throw takeFromExternrefTable0(ret[2]);\n        }\n        deferred2_0 = ptr1;\n        deferred2_1 = len1;\n        return getStringFromWasm0(ptr1, len1);\n      } finally {\n        wasm.__wbindgen_free(deferred2_0, deferred2_1, 1);\n      }\n    }\n    /**\n     * @param {string} data\n     * @returns {string}\n     */\n  }, {\n    key: \"encrypt\",\n    value: function encrypt(data) {\n      var deferred3_0;\n      var deferred3_1;\n      try {\n        var ptr0 = passStringToWasm0(data, wasm.__wbindgen_malloc, wasm.__wbindgen_realloc);\n        var len0 = WASM_VECTOR_LEN;\n        var ret = wasm.midaircode_encrypt(this.__wbg_ptr, ptr0, len0);\n        var ptr2 = ret[0];\n        var len2 = ret[1];\n        if (ret[3]) {\n          ptr2 = 0;\n          len2 = 0;\n          throw takeFromExternrefTable0(ret[2]);\n        }\n        deferred3_0 = ptr2;\n        deferred3_1 = len2;\n        return getStringFromWasm0(ptr2, len2);\n      } finally {\n        wasm.__wbindgen_free(deferred3_0, deferred3_1, 1);\n      }\n    }\n    /**\n     * @param {string} data\n     * @returns {string}\n     */\n  }, {\n    key: \"decrypt\",\n    value: function decrypt(data) {\n      var deferred3_0;\n      var deferred3_1;\n      try {\n        var ptr0 = passStringToWasm0(data, wasm.__wbindgen_malloc, wasm.__wbindgen_realloc);\n        var len0 = WASM_VECTOR_LEN;\n        var ret = wasm.midaircode_decrypt(this.__wbg_ptr, ptr0, len0);\n        var ptr2 = ret[0];\n        var len2 = ret[1];\n        if (ret[3]) {\n          ptr2 = 0;\n          len2 = 0;\n          throw takeFromExternrefTable0(ret[2]);\n        }\n        deferred3_0 = ptr2;\n        deferred3_1 = len2;\n        return getStringFromWasm0(ptr2, len2);\n      } finally {\n        wasm.__wbindgen_free(deferred3_0, deferred3_1, 1);\n      }\n    }\n  }]);\n}();\nfunction __wbindgen_init_externref_table() {\n  var table = wasm.__wbindgen_export_0;\n  var offset = table.grow(4);\n  table.set(0, undefined);\n  table.set(offset + 0, undefined);\n  table.set(offset + 1, null);\n  table.set(offset + 2, true);\n  table.set(offset + 3, false);\n  ;\n}\n;\nfunction __wbindgen_json_serialize(arg0, arg1) {\n  var obj = arg1;\n  var ret = JSON.stringify(obj === undefined ? null : obj);\n  var ptr1 = passStringToWasm0(ret, wasm.__wbindgen_malloc, wasm.__wbindgen_realloc);\n  var len1 = WASM_VECTOR_LEN;\n  getDataViewMemory0().setInt32(arg0 + 4 * 1, len1, true);\n  getDataViewMemory0().setInt32(arg0 + 4 * 0, ptr1, true);\n}\n;\nfunction __wbindgen_string_new(arg0, arg1) {\n  var ret = getStringFromWasm0(arg0, arg1);\n  return ret;\n}\n;\nfunction __wbindgen_throw(arg0, arg1) {\n  throw new Error(getStringFromWasm0(arg0, arg1));\n}\n;\n\n//# sourceURL=webpack://midAirCodeLib/./pkg/mid_air_code_lib_bg.js?");

/***/ }),

/***/ "./pkg/mid_air_code_lib_bg.wasm":
/*!**************************************!*\
  !*** ./pkg/mid_air_code_lib_bg.wasm ***!
  \**************************************/
/***/ ((module, exports, __webpack_require__) => {

eval("/* harmony import */ var WEBPACK_IMPORTED_MODULE_0 = __webpack_require__(/*! ./mid_air_code_lib_bg.js */ \"./pkg/mid_air_code_lib_bg.js\");\nmodule.exports = __webpack_require__.v(exports, module.id, \"faae93322ab270381074\", {\n\t\"./mid_air_code_lib_bg.js\": {\n\t\t\"__wbindgen_string_new\": WEBPACK_IMPORTED_MODULE_0.__wbindgen_string_new,\n\t\t\"__wbindgen_json_serialize\": WEBPACK_IMPORTED_MODULE_0.__wbindgen_json_serialize,\n\t\t\"__wbindgen_throw\": WEBPACK_IMPORTED_MODULE_0.__wbindgen_throw,\n\t\t\"__wbindgen_init_externref_table\": WEBPACK_IMPORTED_MODULE_0.__wbindgen_init_externref_table\n\t}\n});\n\n//# sourceURL=webpack://midAirCodeLib/./pkg/mid_air_code_lib_bg.wasm?");

/***/ })

/******/ 	});
/************************************************************************/
/******/ 	// The module cache
/******/ 	var __webpack_module_cache__ = {};
/******/ 	
/******/ 	// The require function
/******/ 	function __webpack_require__(moduleId) {
/******/ 		// Check if module is in cache
/******/ 		var cachedModule = __webpack_module_cache__[moduleId];
/******/ 		if (cachedModule !== undefined) {
/******/ 			return cachedModule.exports;
/******/ 		}
/******/ 		// Create a new module (and put it into the cache)
/******/ 		var module = __webpack_module_cache__[moduleId] = {
/******/ 			id: moduleId,
/******/ 			// no module.loaded needed
/******/ 			exports: {}
/******/ 		};
/******/ 	
/******/ 		// Execute the module function
/******/ 		__webpack_modules__[moduleId](module, module.exports, __webpack_require__);
/******/ 	
/******/ 		// Return the exports of the module
/******/ 		return module.exports;
/******/ 	}
/******/ 	
/************************************************************************/
/******/ 	/* webpack/runtime/async module */
/******/ 	(() => {
/******/ 		var webpackQueues = typeof Symbol === "function" ? Symbol("webpack queues") : "__webpack_queues__";
/******/ 		var webpackExports = typeof Symbol === "function" ? Symbol("webpack exports") : "__webpack_exports__";
/******/ 		var webpackError = typeof Symbol === "function" ? Symbol("webpack error") : "__webpack_error__";
/******/ 		var resolveQueue = (queue) => {
/******/ 			if(queue && queue.d < 1) {
/******/ 				queue.d = 1;
/******/ 				queue.forEach((fn) => (fn.r--));
/******/ 				queue.forEach((fn) => (fn.r-- ? fn.r++ : fn()));
/******/ 			}
/******/ 		}
/******/ 		var wrapDeps = (deps) => (deps.map((dep) => {
/******/ 			if(dep !== null && typeof dep === "object") {
/******/ 				if(dep[webpackQueues]) return dep;
/******/ 				if(dep.then) {
/******/ 					var queue = [];
/******/ 					queue.d = 0;
/******/ 					dep.then((r) => {
/******/ 						obj[webpackExports] = r;
/******/ 						resolveQueue(queue);
/******/ 					}, (e) => {
/******/ 						obj[webpackError] = e;
/******/ 						resolveQueue(queue);
/******/ 					});
/******/ 					var obj = {};
/******/ 					obj[webpackQueues] = (fn) => (fn(queue));
/******/ 					return obj;
/******/ 				}
/******/ 			}
/******/ 			var ret = {};
/******/ 			ret[webpackQueues] = x => {};
/******/ 			ret[webpackExports] = dep;
/******/ 			return ret;
/******/ 		}));
/******/ 		__webpack_require__.a = (module, body, hasAwait) => {
/******/ 			var queue;
/******/ 			hasAwait && ((queue = []).d = -1);
/******/ 			var depQueues = new Set();
/******/ 			var exports = module.exports;
/******/ 			var currentDeps;
/******/ 			var outerResolve;
/******/ 			var reject;
/******/ 			var promise = new Promise((resolve, rej) => {
/******/ 				reject = rej;
/******/ 				outerResolve = resolve;
/******/ 			});
/******/ 			promise[webpackExports] = exports;
/******/ 			promise[webpackQueues] = (fn) => (queue && fn(queue), depQueues.forEach(fn), promise["catch"](x => {}));
/******/ 			module.exports = promise;
/******/ 			body((deps) => {
/******/ 				currentDeps = wrapDeps(deps);
/******/ 				var fn;
/******/ 				var getResult = () => (currentDeps.map((d) => {
/******/ 					if(d[webpackError]) throw d[webpackError];
/******/ 					return d[webpackExports];
/******/ 				}))
/******/ 				var promise = new Promise((resolve) => {
/******/ 					fn = () => (resolve(getResult));
/******/ 					fn.r = 0;
/******/ 					var fnQueue = (q) => (q !== queue && !depQueues.has(q) && (depQueues.add(q), q && !q.d && (fn.r++, q.push(fn))));
/******/ 					currentDeps.map((dep) => (dep[webpackQueues](fnQueue)));
/******/ 				});
/******/ 				return fn.r ? promise : getResult();
/******/ 			}, (err) => ((err ? reject(promise[webpackError] = err) : outerResolve(exports)), resolveQueue(queue)));
/******/ 			queue && queue.d < 0 && (queue.d = 0);
/******/ 		};
/******/ 	})();
/******/ 	
/******/ 	/* webpack/runtime/define property getters */
/******/ 	(() => {
/******/ 		// define getter functions for harmony exports
/******/ 		__webpack_require__.d = (exports, definition) => {
/******/ 			for(var key in definition) {
/******/ 				if(__webpack_require__.o(definition, key) && !__webpack_require__.o(exports, key)) {
/******/ 					Object.defineProperty(exports, key, { enumerable: true, get: definition[key] });
/******/ 				}
/******/ 			}
/******/ 		};
/******/ 	})();
/******/ 	
/******/ 	/* webpack/runtime/global */
/******/ 	(() => {
/******/ 		__webpack_require__.g = (function() {
/******/ 			if (typeof globalThis === 'object') return globalThis;
/******/ 			try {
/******/ 				return this || new Function('return this')();
/******/ 			} catch (e) {
/******/ 				if (typeof window === 'object') return window;
/******/ 			}
/******/ 		})();
/******/ 	})();
/******/ 	
/******/ 	/* webpack/runtime/hasOwnProperty shorthand */
/******/ 	(() => {
/******/ 		__webpack_require__.o = (obj, prop) => (Object.prototype.hasOwnProperty.call(obj, prop))
/******/ 	})();
/******/ 	
/******/ 	/* webpack/runtime/make namespace object */
/******/ 	(() => {
/******/ 		// define __esModule on exports
/******/ 		__webpack_require__.r = (exports) => {
/******/ 			if(typeof Symbol !== 'undefined' && Symbol.toStringTag) {
/******/ 				Object.defineProperty(exports, Symbol.toStringTag, { value: 'Module' });
/******/ 			}
/******/ 			Object.defineProperty(exports, '__esModule', { value: true });
/******/ 		};
/******/ 	})();
/******/ 	
/******/ 	/* webpack/runtime/wasm loading */
/******/ 	(() => {
/******/ 		__webpack_require__.v = (exports, wasmModuleId, wasmModuleHash, importsObj) => {
/******/ 		
/******/ 			var req = fetch(__webpack_require__.p + "" + wasmModuleHash + ".module.wasm");
/******/ 			var fallback = () => (req
/******/ 				.then((x) => (x.arrayBuffer()))
/******/ 				.then((bytes) => (WebAssembly.instantiate(bytes, importsObj)))
/******/ 				.then((res) => (Object.assign(exports, res.instance.exports))));
/******/ 			return req.then((res) => {
/******/ 				if (typeof WebAssembly.instantiateStreaming === "function") {
/******/ 		
/******/ 					return WebAssembly.instantiateStreaming(res, importsObj)
/******/ 						.then(
/******/ 							(res) => (Object.assign(exports, res.instance.exports)),
/******/ 							(e) => {
/******/ 								if(res.headers.get("Content-Type") !== "application/wasm") {
/******/ 									console.warn("`WebAssembly.instantiateStreaming` failed because your server does not serve wasm with `application/wasm` MIME type. Falling back to `WebAssembly.instantiate` which is slower. Original error:\n", e);
/******/ 									return fallback();
/******/ 								}
/******/ 								throw e;
/******/ 							}
/******/ 						);
/******/ 				}
/******/ 				return fallback();
/******/ 			});
/******/ 		};
/******/ 	})();
/******/ 	
/******/ 	/* webpack/runtime/publicPath */
/******/ 	(() => {
/******/ 		var scriptUrl;
/******/ 		if (__webpack_require__.g.importScripts) scriptUrl = __webpack_require__.g.location + "";
/******/ 		var document = __webpack_require__.g.document;
/******/ 		if (!scriptUrl && document) {
/******/ 			if (document.currentScript && document.currentScript.tagName.toUpperCase() === 'SCRIPT')
/******/ 				scriptUrl = document.currentScript.src;
/******/ 			if (!scriptUrl) {
/******/ 				var scripts = document.getElementsByTagName("script");
/******/ 				if(scripts.length) {
/******/ 					var i = scripts.length - 1;
/******/ 					while (i > -1 && (!scriptUrl || !/^http(s?):/.test(scriptUrl))) scriptUrl = scripts[i--].src;
/******/ 				}
/******/ 			}
/******/ 		}
/******/ 		// When supporting browsers where an automatic publicPath is not supported you must specify an output.publicPath manually via configuration
/******/ 		// or pass an empty string ("") and set the __webpack_public_path__ variable from your code to use your own logic.
/******/ 		if (!scriptUrl) throw new Error("Automatic publicPath is not supported in this browser");
/******/ 		scriptUrl = scriptUrl.replace(/#.*$/, "").replace(/\?.*$/, "").replace(/\/[^\/]+$/, "/");
/******/ 		__webpack_require__.p = scriptUrl;
/******/ 	})();
/******/ 	
/************************************************************************/
/******/ 	
/******/ 	// startup
/******/ 	// Load entry module and return exports
/******/ 	// This entry module can't be inlined because the eval devtool is used.
/******/ 	var __webpack_exports__ = __webpack_require__("./js/index.js");
/******/ 	
/******/ 	return __webpack_exports__;
/******/ })()
;
});