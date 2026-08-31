(function () {
    window.Architecture3D = {
        graphInstance: null,
        dotNetRef: null,
        rawGraphData: null,
        autoRotate: true,
        angle: 0,
        distance: 400,
        chaosNodeId: null,

        init: function (containerId, graphData, dotNetHelper) {
            var container = document.getElementById(containerId);
            if (!container) return;

            this.dotNetRef = dotNetHelper;
            this.rawGraphData = typeof graphData === 'string' ? JSON.parse(graphData) : graphData;

            // Clear existing
            container.innerHTML = '';

            var self = this;
            var width = container.clientWidth || window.innerWidth;
            var height = container.clientHeight || 650;

            // Initialize ForceGraph3D
            this.graphInstance = ForceGraph3D()(container)
                .width(width)
                .height(height)
                .backgroundColor('#07020D')
                .graphData(this.rawGraphData)
                .nodeId('id')
                .nodeLabel(function (node) {
                    return '<div style="background: rgba(15,7,28,0.95); border: 1px solid #78419B; border-radius: 8px; padding: 8px 12px; color: #fff; font-family: sans-serif; box-shadow: 0 0 15px rgba(248,167,25,0.4);">' +
                        '<div style="font-weight: 800; color: #F8A719; font-size: 14px;">' + (node.name || node.id) + '</div>' +
                        '<div style="font-size: 11px; color: #C4B5FD; margin-top: 2px;">' + (node.domain || 'Domain Core') + ' • ' + (node.tier || 'Component') + '</div>' +
                        (node.sigStars ? '<div style="font-size: 11px; color: #F8A719; margin-top: 4px;">⭐ ' + node.sigStars.toFixed(1) + ' / 5.0 Maintainability</div>' : '') +
                        (node.threats ? '<div style="font-size: 11px; color: #EF4444; margin-top: 2px;">🛡️ ' + node.threats + ' Active STRIDE Threats</div>' : '') +
                        '</div>';
                })
                .nodeThreeObject(function (node) {
                    var group = new THREE.Group();
                    var isChaos = self.chaosNodeId === node.id;
                    var color = isChaos ? 0xEF4444 : (node.color || 0x78419B);
                    var radius = node.val || 8;

                    // Central Glowing Sphere
                    var geometry = new THREE.SphereGeometry(radius, 24, 24);
                    var material = new THREE.MeshStandardMaterial({
                        color: color,
                        emissive: color,
                        emissiveIntensity: isChaos ? 0.9 : 0.45,
                        roughness: 0.2,
                        metalness: 0.8
                    });
                    var sphere = new THREE.Mesh(geometry, material);
                    group.add(sphere);

                    // Outer Hologram Halo Ring
                    var ringGeo = new THREE.RingGeometry(radius * 1.3, radius * 1.5, 32);
                    var ringMat = new THREE.MeshBasicMaterial({
                        color: color,
                        side: THREE.DoubleSide,
                        transparent: true,
                        opacity: isChaos ? 0.8 : 0.35
                    });
                    var ring = new THREE.Mesh(ringGeo, ringMat);
                    ring.rotation.x = Math.PI / 2;
                    group.add(ring);

                    // 2D Text Sprite Label
                    var canvas = document.createElement('canvas');
                    var context = canvas.getContext('2d');
                    canvas.width = 256;
                    canvas.height = 64;
                    context.font = 'Bold 22px Inter, sans-serif';
                    context.fillStyle = isChaos ? '#EF4444' : '#FFFFFF';
                    context.textAlign = 'center';
                    context.fillText(node.name || node.id, 128, 40);

                    var texture = new THREE.CanvasTexture(canvas);
                    var spriteMaterial = new THREE.SpriteMaterial({ map: texture, transparent: true });
                    var sprite = new THREE.Sprite(spriteMaterial);
                    sprite.position.set(0, radius + 8, 0);
                    sprite.scale.set(30, 7.5, 1);
                    group.add(sprite);

                    return group;
                })
                // Link styling & Active Moving Particles
                .linkColor(function (link) {
                    if (self.chaosNodeId && (link.source.id === self.chaosNodeId || link.target.id === self.chaosNodeId)) {
                        return '#EF4444';
                    }
                    return link.color || 'rgba(157, 78, 221, 0.4)';
                })
                .linkWidth(function (link) {
                    return (self.chaosNodeId && (link.source.id === self.chaosNodeId || link.target.id === self.chaosNodeId)) ? 3 : 1.5;
                })
                .linkCurvature(0.2)
                .linkDirectionalParticles(3)
                .linkDirectionalParticleSpeed(0.008)
                .linkDirectionalParticleWidth(2.5)
                .linkDirectionalParticleColor(function (link) {
                    if (self.chaosNodeId && (link.source.id === self.chaosNodeId || link.target.id === self.chaosNodeId)) {
                        return '#EF4444';
                    }
                    return link.particleColor || '#F8A719';
                })
                .onNodeClick(function (node) {
                    // Smooth Fly-To Trajectory
                    var dist = 80;
                    var distRatio = 1 + dist / Math.hypot(node.x, node.y, node.z);
                    self.graphInstance.cameraPosition(
                        { x: node.x * distRatio, y: node.y * distRatio + 20, z: node.z * distRatio },
                        node,
                        1200
                    );

                    // Notify Blazor for 360 cockpit inspection
                    if (self.dotNetRef) {
                        self.dotNetRef.invokeMethodAsync('OnNodeSelectedIn3D', node.id);
                    }
                })
                .onBackgroundClick(function () {
                    if (self.dotNetRef) {
                        self.dotNetRef.invokeMethodAsync('OnBackgroundClickedIn3D');
                    }
                });

            // Set Initial Camera Position
            this.graphInstance.cameraPosition({ x: 0, y: 150, z: 380 });

            // Start Auto-Orbit Loop
            this.startAutoOrbit();

            // Resize Observer
            window.addEventListener('resize', function () {
                if (self.graphInstance && container) {
                    self.graphInstance.width(container.clientWidth);
                }
            });
        },

        startAutoOrbit: function () {
            var self = this;
            function orbit() {
                if (self.autoRotate && self.graphInstance && !self.chaosNodeId) {
                    self.angle += 0.002;
                    var x = self.distance * Math.sin(self.angle);
                    var z = self.distance * Math.cos(self.angle);
                    self.graphInstance.cameraPosition({ x: x, y: 120, z: z });
                }
                requestAnimationFrame(orbit);
            }
            orbit();
        },

        toggleAutoRotate: function (enabled) {
            this.autoRotate = enabled;
        },

        focusNode: function (nodeId) {
            if (!this.graphInstance || !this.rawGraphData) return;
            var node = this.rawGraphData.nodes.find(function (n) { return n.id === nodeId; });
            if (node) {
                this.graphInstance.cameraPosition(
                    { x: (node.x || 0) + 60, y: (node.y || 0) + 30, z: (node.z || 0) + 60 },
                    node,
                    1200
                );
            }
        },

        resetCamera: function () {
            if (this.graphInstance) {
                this.chaosNodeId = null;
                this.graphInstance.cameraPosition({ x: 0, y: 150, z: 380 }, { x: 0, y: 0, z: 0 }, 1000);
                this.graphInstance.refresh();
            }
        },

        simulateChaosOutage: function (nodeId) {
            this.chaosNodeId = nodeId;
            if (this.graphInstance) {
                this.graphInstance.refresh();
                this.focusNode(nodeId);
            }
        },

        clearChaosOutage: function () {
            this.chaosNodeId = null;
            if (this.graphInstance) {
                this.graphInstance.refresh();
            }
        },

        filterGraph: function (filterOptions) {
            if (!this.graphInstance || !this.rawGraphData) return;

            var showDbs = filterOptions.showDatabases;
            var showApis = filterOptions.showExternalApis;
            var showCaps = filterOptions.showCapabilities;

            var filteredNodes = this.rawGraphData.nodes.filter(function (n) {
                if (n.type === 'Database' && !showDbs) return false;
                if (n.type === 'ExternalApi' && !showApis) return false;
                if (n.type === 'Capability' && !showCaps) return false;
                return true;
            });

            var nodeIds = new Set(filteredNodes.map(function (n) { return n.id; }));
            var filteredLinks = this.rawGraphData.links.filter(function (l) {
                var s = typeof l.source === 'object' ? l.source.id : l.source;
                var t = typeof l.target === 'object' ? l.target.id : l.target;
                return nodeIds.has(s) && nodeIds.has(t);
            });

            this.graphInstance.graphData({
                nodes: filteredNodes,
                links: filteredLinks
            });
        }
    };
})();
