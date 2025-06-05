// shaderRenderer.js
export function initShader(canvasId, dotNetHelper) {
    const canvas = document.getElementById(canvasId);
    const gl = canvas.getContext("webgl2");

    if (!gl) {
        console.error("WebGL2 not supported");
        return;
    }

    // Set up resizing
    const resizeCanvas = () => {
        const displayWidth = canvas.clientWidth;
        const displayHeight = canvas.clientHeight;

        if (canvas.width !== displayWidth || canvas.height !== displayHeight) {
            canvas.width = displayWidth;
            canvas.height = displayHeight;
            gl.viewport(0, 0, gl.canvas.width, gl.canvas.height);
        }
    };

    window.addEventListener('resize', resizeCanvas);
    resizeCanvas();

    // Vertex shader program - simple passthrough
    const vsSource = `#version 300 es
    in vec4 aPosition;
    void main() {
        gl_Position = aPosition;
    }`;

    // Fragment shader program - your specific shader code
    const fsSource = `#version 300 es
    precision highp float;
    out vec4 fragColor;
    
    uniform vec2 resolution;
    uniform float time;
    
    void main() {
        vec4 FC = vec4(gl_FragCoord.xy, 0.0, 1.0);
        vec2 r = resolution.xy;
        vec4 o = vec4(0.0);
        
        vec2 p = (FC.xy*2.-r)/r.y/.7;
        vec2 d = vec2(-1,1);
        vec2 c = p*mat2(1,1,d/(.1+5./dot(5.*p-d,5.*p-d)));
        vec2 v = c;
        
        v *= mat2(cos(log(length(v))+time*.2+vec4(0,33,11,0)))*5.;
        
        for(float i = 0.0; i < 9.0; i += 1.0) {
            v += .7*sin(v.yx*i+time)/i+.5;
            o += sin(vec4(v.x, v.y, v.y, v.x))+1.0;
        }
        
        o = 1.-exp(-exp(c.x*vec4(.6,-.4,-1,0))/o/(.1+.1*pow(length(sin(v/.3)*.2+c*vec2(1,2))-1.,2.))/(1.+7.*exp(.3*c.y-dot(c,c)))/(.03+abs(length(p)-.7))*.2);
        
        fragColor = o;
    }`;

    // Initialize shader program
    const shaderProgram = initShaderProgram(gl, vsSource, fsSource);

    if (!shaderProgram) {
        return;
    }

    // Shader program info
    const programInfo = {
        program: shaderProgram,
        attribLocations: {
            position: gl.getAttribLocation(shaderProgram, 'aPosition')
        },
        uniformLocations: {
            resolution: gl.getUniformLocation(shaderProgram, 'resolution'),
            time: gl.getUniformLocation(shaderProgram, 'time')
        }
    };

    // Create buffer for the positions
    const positionBuffer = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, positionBuffer);

    // Four vertices for a full-screen quad
    const positions = [
        -1.0, -1.0,
        1.0, -1.0,
        -1.0,  1.0,
        1.0,  1.0
    ];

    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(positions), gl.STATIC_DRAW);

    // Setup vertex attribute pointer
    gl.vertexAttribPointer(
        programInfo.attribLocations.position,
        2,        // size (2 components per vertex)
        gl.FLOAT, // type
        false,    // normalize
        0,        // stride
        0         // offset
    );
    gl.enableVertexAttribArray(programInfo.attribLocations.position);

    let startTime = Date.now();

    // Draw scene
    function render() {
        // Calculate time
        const currentTime = (Date.now() - startTime) * 0.001; // seconds

        // Clear the canvas
        gl.clearColor(0.0, 0.0, 0.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        // Use our shader program
        gl.useProgram(programInfo.program);

        // Set uniforms
        gl.uniform2f(programInfo.uniformLocations.resolution, gl.canvas.width, gl.canvas.height);
        gl.uniform1f(programInfo.uniformLocations.time, currentTime);

        // Draw the quad
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

        // Request next frame
        requestAnimationFrame(render);
    }

    // Start the rendering loop
    render();
}

// Helper function to initialize a shader program from vertex and fragment shader sources
function initShaderProgram(gl, vsSource, fsSource) {
    const vertexShader = loadShader(gl, gl.VERTEX_SHADER, vsSource);
    const fragmentShader = loadShader(gl, gl.FRAGMENT_SHADER, fsSource);

    if (!vertexShader || !fragmentShader) {
        return null;
    }

    // Create the shader program
    const shaderProgram = gl.createProgram();
    gl.attachShader(shaderProgram, vertexShader);
    gl.attachShader(shaderProgram, fragmentShader);
    gl.linkProgram(shaderProgram);

    // Check if shader program linked successfully
    if (!gl.getProgramParameter(shaderProgram, gl.LINK_STATUS)) {
        console.error(`Unable to initialize the shader program: ${gl.getProgramInfoLog(shaderProgram)}`);
        return null;
    }

    return shaderProgram;
}

// Helper function to load and compile a shader
function loadShader(gl, type, source) {
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);

    // Check if shader compiled successfully
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        console.error(`An error occurred compiling the shaders: ${gl.getShaderInfoLog(shader)}`);
        gl.deleteShader(shader);
        return null;
    }

    return shader;
}