
/** @enum {number} */
let buttonStates = {
    CREATE: 0,
    CREATING: 1,
    CREATED: 2
}

let buttonState = buttonStates.CREATE;

/**
 * @param {buttonStates} state 
 */
function setButtonState(state) {
    const button = $('#create');
    button.removeClass('active');
    $('.network-name').hide();
    $('.statusIcon').hide();
    button.attr('href', '#');
    buttonState = state;
    switch(state) {
        case buttonStates.CREATE:
            $('#createServerText').show();
            $('#cloudIcon').show();
            $('#recaptcha').show();
            break;

        case buttonStates.CREATING:
            $('#creatingServerText').show();
            $('#spinnerIcon').show();
            $('#recaptcha').show();
            button.addClass('active');
            break;

        case buttonStates.CREATED:
            $('#createdServerText').show();
            $('#cloudIcon').show();
            $('#recaptcha').hide();
            button.attr('href', '/server');
            break;
    }
}

function resetCreate() {
    grecaptcha.reset();
    setButtonState(buttonStates.CREATE);
}

$('#create[data-isCreated=False]').click(function() {
    if (buttonState === buttonStates.CREATE) {
        setButtonState(buttonStates.CREATING);
        grecaptcha.execute();
    }
    if (buttonState !== buttonStates.CREATED) {
        return false;
    }
});

function recaptchaVerified(token) {
    if (grecaptcha.getResponse() === "") {
        console.log("recaptcha not verified");
        resetCreate();
        return;
    }
    console.log('verified!');
    createServer(token);
}

function createServer() {
    $('#createForm').submit();
}

let mapInitialized = false;
const regionSelectorElem = document.getElementById("regionSelector");

regionSelectorElem.addEventListener("change", updateMarkers);

const locations = regions.map(region => {
    return {
        name: region.displayName,
        coords: [region.latitude, region.longitude],
        region: region.region,
        marker: null
    }
});

function openDialog() {
    if (!mapInitialized) {
        initMap();
        mapInitialized = true;
    }

    updateMarkers();
}

function updateMarkers() {
    locations.forEach(location => {
        if (location.marker === null) {
            return;
        }
        if (location.region === regionSelectorElem.value) {
            location.marker._icon.style.filter = "hue-rotate(120deg)"
            location.marker.setZIndexOffset(1000);
        } else {
            location.marker._icon.style.filter = null;
            location.marker.setZIndexOffset(0);
        }
    });
}

function initMap() {
    const map = L.map('map', {
        maxBounds: L.latLngBounds(L.latLng(-90, -180), L.latLng(90, 180))
    }).setView([0, 0], 2)

    L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}' + (L.Browser.retina ? '@2x.png' : '.png'), {
        attribution: '&copy; <a href="http://www.openstreetmap.org/copyright">OpenStreetMap</a>, &copy; <a href="https://carto.com/attributions">CARTO</a>',
        subdomains: 'abcd',
        maxZoom: 5,
        minZoom: 2,
        noWrap: true,
        bounds: L.latLngBounds(L.latLng(-90, -180), L.latLng(90, 180)),
    }).addTo(map);

// Add markers for each location
    locations.forEach(location => {
        const marker = L.marker(location.coords, {riseOnHover: true})
            .bindTooltip(location.name)
            .addTo(map);
        location.marker = marker;

        // Add click event listener
        marker.on('click', function () {
            regionSelectorElem.value = location.region;
            updateMarkers();
            console.log("Clicked on: ", location);
            // You can perform any action you want here when the marker is clicked
            // For example, open a popup with more information about the location
            bootstrap.Modal.getOrCreateInstance(document.getElementById('mapModal')).hide();
        });
    });
    mapInitialized = true;
}

document.getElementById('mapModal').addEventListener('shown.bs.modal', openDialog);