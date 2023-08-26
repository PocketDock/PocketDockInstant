
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